import { synchronize, hasUnsyncedChanges } from '@nozbe/watermelondb/sync';
import { Model } from '@nozbe/watermelondb';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import RegistroPresenca, { StatusPresencaLocal } from '../../database/models/RegistroPresenca';
import Turma from '../../database/models/Turma';
import { api } from '../api';
import { AxiosError } from 'axios';
import { chamadasService } from '../chamadasService';

// ── Tipos do payload PUSH (enviado à API .NET) ──────────────────────────────

interface TurmaSyncDto {
  id: string;
  nome: string;
  turno: string;
  anoLetivo: number;
}

interface RegistroPresencaSyncDto {
  id: string;
  alunoId: string;
  turmaId: string;
  data: number;
  status: string;
}

interface AlunoOfflineSyncDto {
  id: string;
  nome: string;
  turmaId: string;
}

interface SyncPushPayload {
  changes: {
    turmas: {
      created: TurmaSyncDto[];
      updated: TurmaSyncDto[];
      deleted: string[];
    };
    alunos: {
      created: AlunoOfflineSyncDto[];
    };
    registrosPresenca: {
      created: RegistroPresencaSyncDto[];
      updated: RegistroPresencaSyncDto[];
      deleted: string[];
    };
  };
  lastPulledAt: number;
}

// ── Tipos do payload PULL (recebido da API .NET) ────────────────────────────

interface SyncPullResponse {
  changes: {
    turmas: SyncTableChanges;
    alunos: SyncTableChanges;
    registros_presenca: SyncTableChanges;
  };
  timestamp: number;
}

interface SyncTableChanges {
  created: Record<string, any>[];
  updated: Record<string, any>[];
  deleted: string[];
}

export interface SyncRejeicao {
  idExterno: string;
  motivo: string;
}

export interface SyncResult {
  sucesso: boolean;
  rejeicoes: SyncRejeicao[];
  erro?: string;
}

/**
 * Erro levantado dentro do pushChanges quando o backend retorna 422.
 * Faz o synchronize() falhar para que o WatermelonDB NÃO marque as demais
 * alterações do batch como sincronizadas; elas serão reenviadas na próxima
 * tentativa, enquanto os registros rejeitados já foram revertidos localmente.
 */
class SyncRejeitadoError extends Error {
  constructor(
    public readonly rejeicoes: SyncRejeicao[],
    message = 'Sync rejeitado pelo servidor'
  ) {
    super(message);
    this.name = 'SyncRejeitadoError';
  }
}

// ── Transformações snake_case ↔ camelCase ────────────────────────────────────

function transformarTurmaPush(raw: Record<string, any>): TurmaSyncDto {
  return {
    id: raw.id,
    nome: raw.nome,
    turno: raw.turno,
    anoLetivo: raw.ano_letivo,
  };
}

function transformarAlunoPush(raw: Record<string, any>): AlunoOfflineSyncDto {
  return {
    id: raw.id,
    nome: raw.nome,
    turmaId: raw.turma_id,
  };
}

function transformarRegistroPush(raw: Record<string, any>): RegistroPresencaSyncDto {
  return {
    id: raw.id,
    alunoId: raw.aluno_id,
    turmaId: raw.turma_id,
    data: raw.data,
    status: raw.status,
  };
}

/**
 * Pull: API .NET → WatermelonDB.
 * O backend envia turma_id via [JsonPropertyName], mas normalizamos
 * aqui como safety net caso o serializer emita turmaId (camelCase).
 */
function normalizarAluno(raw: Record<string, any>): Record<string, any> {
  return {
    id: raw.id,
    nome: raw.nome,
    turma_id: raw.turma_id ?? raw.turmaId,
    server_id: raw.server_id ?? raw.id,
    matricula: raw.matricula ?? null,
    faltas_consecutivas_atuais: raw.faltas_consecutivas_atuais ?? 0,
    total_faltas: raw.total_faltas ?? 0,
  };
}

function normalizarTurma(raw: Record<string, any>): Record<string, any> {
  return {
    id: raw.id,
    nome: raw.nome,
    turno: raw.turno,
    ano_letivo: raw.ano_letivo ?? raw.anoLetivo ?? 0,
    server_id: raw.id,
  };
}

// ── Função principal de sincronização ────────────────────────────────────────

/**
 * Executa o ciclo completo de sync usando a função `synchronize()` nativa
 * do WatermelonDB.
 *
 * PULL: Baixa turmas e alunos do servidor → WatermelonDB/SQLite local.
 * PUSH: Envia turmas e registros de presença criados offline → API .NET.
 *
 * RESILIÊNCIA: Se o axios rejeitar (sem Wi-Fi, timeout, 5xx),
 * o erro propaga para o `synchronize()`, que aborta o ciclo sem
 * marcar nada como sincronizado. Na próxima tentativa, tudo é reenviado.
 */
export async function syncWithServer(): Promise<SyncResult> {
  let houvePresencaEnviada = false;
  // Registra o timestamp ANTES do sync para garantir que o pull pós-push
  // capture as atualizações feitas durante o push (independente da duração do ciclo)
  const timestampAntesDoCiclo = Date.now() - 5_000;

  try {
    await synchronize({
      database,

      // ── PULL: servidor → celular (turmas + alunos) ────────────────────
      pullChanges: async ({ lastPulledAt }) => {
        const response = await api.get<SyncPullResponse>('/sync/pull', {
          params: { lastPulledAt: lastPulledAt ?? 0 },
        });

        const { changes, timestamp } = response.data;

        const turmasNormalizadas: SyncTableChanges = {
          created: changes.turmas.created.map(normalizarTurma),
          updated: changes.turmas.updated.map(normalizarTurma),
          deleted: changes.turmas.deleted,
        };

        const alunosNormalizados: SyncTableChanges = {
          created: changes.alunos.created.map(normalizarAluno),
          updated: changes.alunos.updated.map(normalizarAluno),
          deleted: changes.alunos.deleted,
        };

        return {
          changes: {
            turmas: turmasNormalizadas,
            alunos: alunosNormalizados,
            registros_presenca: changes.registros_presenca,
          },
          timestamp,
        };
      },

      // ── PUSH: celular → servidor (turmas + registros de presença) ─────
      pushChanges: async ({ changes, lastPulledAt }) => {
        const c = changes as Record<string, any>;

        const turmasCreated = (c['turmas']?.created ?? []) as Record<string, any>[];
        const turmasUpdated = (c['turmas']?.updated ?? []) as Record<string, any>[];
        const turmasDeleted = (c['turmas']?.deleted ?? []) as string[];

        const alunosCreated = (c['alunos']?.created ?? []) as Record<string, any>[];

        const rawCreated = (c['registros_presenca']?.created ?? []) as Record<string, any>[];
        const rawUpdated = (c['registros_presenca']?.updated ?? []) as Record<string, any>[];
        const rawDeleted = (c['registros_presenca']?.deleted ?? []) as string[];

        console.log('[SYNC-PUSH] Delta:', {
          turmasCriadas: turmasCreated.length,
          turmasAtualizadas: turmasUpdated.length,
          alunosCriados: alunosCreated.length,
          presencasCriadas: rawCreated.length,
          presencasAtualizadas: rawUpdated.length,
        });

        const temAlgo =
          turmasCreated.length > 0 || turmasUpdated.length > 0 || turmasDeleted.length > 0 ||
          alunosCreated.length > 0 ||
          rawCreated.length > 0 || rawUpdated.length > 0 || rawDeleted.length > 0;

        if (!temAlgo) {
          console.log('[SYNC-PUSH] Nada a enviar.');
          return;
        }

        const payload: SyncPushPayload = {
          changes: {
            turmas: {
              created: turmasCreated.map(transformarTurmaPush),
              updated: turmasUpdated.map(transformarTurmaPush),
              deleted: turmasDeleted,
            },
            alunos: {
              created: alunosCreated.map(transformarAlunoPush),
            },
            registrosPresenca: {
              created: rawCreated.map(transformarRegistroPush),
              updated: rawUpdated.map(transformarRegistroPush),
              deleted: rawDeleted,
            },
          },
          lastPulledAt: lastPulledAt ?? 0,
        };

        try {
          await api.post('/sync/push', payload);
          houvePresencaEnviada = rawCreated.length > 0 || rawUpdated.length > 0;
        } catch (erro: any) {
          const axiosError = erro as AxiosError<any>;
          if (axiosError.response?.status === 422) {
            const rejeicoes: SyncRejeicao[] = (axiosError.response.data?.rejeicoes ?? []).map(
              (r: any) => ({ idExterno: r.idExterno ?? r.id_externo ?? '', motivo: r.motivo ?? '' })
            );

            console.warn('[SYNC-PUSH] Rejeições do backend:', rejeicoes);

            // Reverte alterações rejeitadas para não bloquear o próximo push.
            await reverterAlteracoesRejeitadas({
              turmasCreated,
              turmasUpdated,
              alunosCreated,
              presencasCreated: rawCreated,
              presencasUpdated: rawUpdated,
              rejeicoes,
            });

            // Lança erro para que o WatermelonDB não marque as demais alterações
            // do batch como sincronizadas. As linhas válidas serão reenviadas na
            // próxima tentativa; as rejeitadas já foram revertidas localmente.
            throw new SyncRejeitadoError(rejeicoes);
          }

          throw erro;
        }
      },

      migrationsEnabledAtVersion: 2,
    });

    // Se enviou presenças, busca os contadores atualizados diretamente via API
    // e atualiza o WatermelonDB local sem passar pelo synchronize().
    // Isso evita o problema de timing: o synchronize() usa o lastPulledAt já avançado
    // (pós-primeiro-ciclo), que é posterior à atualização do servidor feita pelo push.
    if (houvePresencaEnviada) {
      // Segundo ciclo de sync usando o timestamp ANTES do push como lastPulledAt,
      // garantindo que o servidor retorne os contadores atualizados pelo push.
      // O pushChanges é vazio pois não há mais nada a enviar.
      await synchronize({
        database,
        pullChanges: async () => {
          const response = await api.get<SyncPullResponse>('/sync/pull', {
            params: { lastPulledAt: timestampAntesDoCiclo },
          });
          const { changes, timestamp } = response.data;
          return {
            changes: {
              turmas: {
                created: changes.turmas.created.map(normalizarTurma),
                updated: changes.turmas.updated.map(normalizarTurma),
                deleted: changes.turmas.deleted,
              },
              alunos: {
                created: changes.alunos.created.map(normalizarAluno),
                updated: changes.alunos.updated.map(normalizarAluno),
                deleted: changes.alunos.deleted,
              },
              registros_presenca: changes.registros_presenca,
            },
            timestamp,
          };
        },
        pushChanges: async () => { /* nada a enviar */ },
        migrationsEnabledAtVersion: 2,
      });
    }

    return { sucesso: true, rejeicoes: [] };
  } catch (erro: any) {
    if (erro instanceof SyncRejeitadoError) {
      return {
        sucesso: false,
        rejeicoes: erro.rejeicoes,
        erro: `${erro.rejeicoes.length} registro(s) foram rejeitados pelo servidor e revertidos localmente. As alterações válidas serão reenviadas automaticamente.`,
      };
    }

    const mensagem =
      erro?.response?.data?.detail ||
      erro?.message ||
      'Sem conexão. Os registros serão enviados quando houver rede.';

    return { sucesso: false, rejeicoes: [], erro: mensagem };
  }
}

interface ReverterPayload {
  turmasCreated: Record<string, any>[];
  turmasUpdated: Record<string, any>[];
  alunosCreated: Record<string, any>[];
  presencasCreated: Record<string, any>[];
  presencasUpdated: Record<string, any>[];
  rejeicoes: SyncRejeicao[];
}

/**
 * Reverte alterações locais que foram rejeitadas pelo backend (HTTP 422).
 * Isso evita que um registro inválido fique preso no batch atômico de sync,
 * bloqueando alterações válidas subsequentes.
 *
 * Estratégia:
 * - Created rejeitado: destrói o registro local (o servidor nunca o aceitou).
 * - Updated rejeitado: marca como sincronizado para não reenviar; o próximo
 *   pull trará o estado atual do servidor e sobrescreverá o status local.
 */
async function reverterAlteracoesRejeitadas(payload: ReverterPayload): Promise<void> {
  const { turmasCreated, turmasUpdated, alunosCreated, presencasCreated, presencasUpdated, rejeicoes } = payload;

  const idsTurmasCreated = new Set(turmasCreated.map((t) => String(t.id)));
  const idsTurmasUpdated = new Set(turmasUpdated.map((t) => String(t.id)));
  const idsAlunosCreated = new Set(alunosCreated.map((a) => String(a.id)));
  const idsPresencasCreated = new Set(presencasCreated.map((r) => String(r.id)));
  const idsPresencasUpdated = new Set(presencasUpdated.map((r) => String(r.id)));

  for (const rejeicao of rejeicoes) {
    const id = rejeicao.idExterno;
    if (!id) continue;

    try {
      // Created rejeitado: destrói o registro local (o servidor nunca o aceitou).
      if (idsPresencasCreated.has(id)) {
        const registro = await database.get<RegistroPresenca>('registros_presenca').find(id);
        await database.write(async () => {
          await registro.destroyPermanently();
        });
        console.log('[SYNC-RECOVERY] Presença created destruída:', id);
        continue;
      }

      // Updated rejeitado: restaura o status autoritativo do servidor consultando
      // a chamada do dia. Se não for possível recuperar, destrói o registro local.
      if (idsPresencasUpdated.has(id)) {
        await restaurarPresencaDoServidor(id);
        continue;
      }

      if (idsTurmasCreated.has(id)) {
        const turma = await database.get<Turma>('turmas').find(id);
        await database.write(async () => {
          await turma.destroyPermanently();
        });
        console.log('[SYNC-RECOVERY] Turma created destruída:', id);
        continue;
      }

      if (idsTurmasUpdated.has(id)) {
        const turma = await database.get<Turma>('turmas').find(id);
        await database.write(async () => {
          // Não alteramos os dados localmente; apenas marcamos como sincronizado
          // para que o próximo pull do servidor restaure o estado autoritativo.
          marcarComoSincronizado(turma);
        });
        console.log('[SYNC-RECOVERY] Turma updated marcada como sincronizada:', id);
        continue;
      }

      if (idsAlunosCreated.has(id)) {
        const aluno = await database.get<Aluno>('alunos').find(id);
        await database.write(async () => {
          await aluno.destroyPermanently();
        });
        console.log('[SYNC-RECOVERY] Aluno created destruído:', id);
      }
    } catch (erroLocal) {
      console.warn('[SYNC-RECOVERY] Falha ao reverter registro rejeitado:', id, erroLocal);
    }
  }
}

/**
 * Restaura o status de uma presença updated rejeitada a partir do estado
 * autoritativo do servidor. Se a chamada não existir no servidor ou o aluno
 * não constar nela, o registro local é destruído.
 */
async function restaurarPresencaDoServidor(idExterno: string): Promise<void> {
  try {
    const registro = await database.get<RegistroPresenca>('registros_presenca').find(idExterno);
    const turmaId = registro.turmaId;
    const data = registro.data;
    const alunoId = registro.alunoId;

    if (!turmaId || !data) {
      await database.write(async () => {
        await registro.destroyPermanently();
      });
      console.log('[SYNC-RECOVERY] Presença updated destruída (sem turma/data):', idExterno);
      return;
    }

    const chamada = await chamadasService.obterChamadaPorDia(turmaId, data);

    if (!chamada) {
      await database.write(async () => {
        await registro.destroyPermanently();
      });
      console.log('[SYNC-RECOVERY] Presença updated destruída (chamada não encontrada):', idExterno);
      return;
    }

    const statusServidor = chamada.registros.find((r) => r.alunoId === alunoId)?.status;

    if (!statusServidor) {
      await database.write(async () => {
        await registro.destroyPermanently();
      });
      console.log('[SYNC-RECOVERY] Presença updated destruída (aluno não consta na chamada):', idExterno);
      return;
    }

    const statusLocal = mapearStatusServidorParaLocal(statusServidor);

    await database.write(async () => {
      await registro.update((r) => {
        r.status = statusLocal;
        r.sincronizado = true;
      });

      // Marca o registro como sincronizado no próprio WatermelonDB.
      // registro.update() deixa o registro como 'updated' para o sync engine;
      // para evitar reenvio infinito, replicamos o que markLocalChangesAsSynced
      // faz internamente: _status='synced' e _changed=''.
      marcarComoSincronizado(registro);
    });

    console.log('[SYNC-RECOVERY] Presença updated restaurada do servidor:', idExterno, statusServidor);
  } catch (erro) {
    console.warn('[SYNC-RECOVERY] Falha ao restaurar presença do servidor:', idExterno, erro);
    // Se não foi possível obter o estado autoritativo do servidor (por exemplo,
    // por falta de permissão na turma), destrói o registro local para evitar
    // reenvio infinito de uma alteração rejeitada.
    try {
      const registro = await database.get<RegistroPresenca>('registros_presenca').find(idExterno);
      await database.write(async () => {
        await registro.destroyPermanently();
      });
      console.log('[SYNC-RECOVERY] Presença updated destruída após falha de recuperação:', idExterno);
    } catch (erroDestruicao) {
      console.warn('[SYNC-RECOVERY] Falha ao destruir presença após erro de recuperação:', idExterno, erroDestruicao);
    }
  }
}

/**
 * Marca um registro WatermelonDB como sincronizado, limpando os campos
 * internos _status e _changed. Usado no recovery de rejeições 422.
 *
 * Nota: esta é a mesma operação que a função interna markLocalChangesAsSynced
 * realiza. Não há API pública para isso; portanto, acessamos _raw com cuidado.
 */
function marcarComoSincronizado(record: Model): void {
  // @ts-ignore — campos internos do WatermelonDB
  record._raw._status = 'synced';
  // @ts-ignore
  record._raw._changed = '';
}

function mapearStatusServidorParaLocal(status: string): StatusPresencaLocal {
  if (status === 'Ausente') return 'Falta';
  if (['Presente', 'Falta', 'Atraso', 'FaltaJustificada'].includes(status)) {
    return status as StatusPresencaLocal;
  }
  return 'Presente';
}

/**
 * Verifica se existem registros pendentes de sincronização.
 * Usa a API nativa do WatermelonDB (verifica _status interno).
 */
export async function hasPendingSync(): Promise<boolean> {
  return hasUnsyncedChanges({ database });
}
