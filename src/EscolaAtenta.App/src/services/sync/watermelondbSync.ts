import { synchronize, hasUnsyncedChanges } from '@nozbe/watermelondb/sync';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import { api } from '../api';

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
    faltas_consecutivas_atuais: raw.faltas_consecutivas_atuais ?? 0,
    faltas_no_trimestre: raw.faltas_no_trimestre ?? 0,
    total_faltas: raw.total_faltas ?? 0,
    atrasos_no_trimestre: raw.atrasos_no_trimestre ?? 0,
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
export async function syncWithServer(): Promise<void> {
  let houvePresencaEnviada = false;
  // Registra o timestamp ANTES do sync para garantir que o pull pós-push
  // capture as atualizações feitas durante o push (independente da duração do ciclo)
  const timestampAntesDoCiclo = Date.now() - 5_000;

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

      await api.post('/sync/push', payload);
      houvePresencaEnviada = rawCreated.length > 0 || rawUpdated.length > 0;
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
}

/**
 * Verifica se existem registros pendentes de sincronização.
 * Usa a API nativa do WatermelonDB (verifica _status interno).
 */
export async function hasPendingSync(): Promise<boolean> {
  return hasUnsyncedChanges({ database });
}
