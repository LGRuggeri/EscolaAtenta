import { synchronize, hasUnsyncedChanges } from '@nozbe/watermelondb/sync';
import database from '../../database';
import { api } from '../api';

// ── Tipos do payload PUSH (enviado à API .NET) ──────────────────────────────

interface RegistroPresencaSyncDto {
  id: string;
  alunoId: string;
  turmaId: string;
  data: number;
  status: string;
}

interface SyncPushPayload {
  changes: {
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

/**
 * Push: WatermelonDB (snake_case) → API .NET (camelCase)
 */
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
  };
}

// ── Função principal de sincronização ────────────────────────────────────────

/**
 * Executa o ciclo completo de sync usando a função `synchronize()` nativa
 * do WatermelonDB.
 *
 * PULL: Baixa turmas e alunos do servidor → WatermelonDB/SQLite local.
 * PUSH: Envia registros de presença criados offline → API .NET.
 *
 * RESILIÊNCIA: Se o axios rejeitar (sem Wi-Fi, timeout, 5xx),
 * o erro propaga para o `synchronize()`, que aborta o ciclo sem
 * marcar nada como sincronizado. Na próxima tentativa, tudo é reenviado.
 */
export async function syncWithServer(): Promise<void> {
  await synchronize({
    database,

    // ── PULL: servidor → celular (turmas + alunos) ────────────────────
    pullChanges: async ({ lastPulledAt }) => {
      const response = await api.get<SyncPullResponse>('/sync/pull', {
        params: { lastPulledAt: lastPulledAt ?? 0 },
      });

      const { changes, timestamp } = response.data;

      // Normaliza alunos para garantir snake_case na coluna turma_id
      const alunosNormalizados: SyncTableChanges = {
        created: changes.alunos.created.map(normalizarAluno),
        updated: changes.alunos.updated.map(normalizarAluno),
        deleted: changes.alunos.deleted,
      };

      return {
        changes: {
          turmas: changes.turmas,
          alunos: alunosNormalizados,
          registros_presenca: changes.registros_presenca,
        },
        timestamp,
      };
    },

    // ── PUSH: celular → servidor (registros de presença) ──────────────
    pushChanges: async ({ changes, lastPulledAt }) => {
      const rawCreated = (changes.registros_presenca?.created ?? []) as Record<string, any>[];
      const rawUpdated = (changes.registros_presenca?.updated ?? []) as Record<string, any>[];
      const rawDeleted = (changes.registros_presenca?.deleted ?? []) as string[];

      if (rawCreated.length === 0 && rawUpdated.length === 0 && rawDeleted.length === 0) {
        return;
      }

      const payload: SyncPushPayload = {
        changes: {
          registrosPresenca: {
            created: rawCreated.map(transformarRegistroPush),
            updated: rawUpdated.map(transformarRegistroPush),
            deleted: rawDeleted,
          },
        },
        lastPulledAt: lastPulledAt ?? 0,
      };

      await api.post('/sync/push', payload);
    },

    migrationsEnabledAtVersion: 1,
  });
}

/**
 * Verifica se existem registros pendentes de sincronização.
 * Usa a API nativa do WatermelonDB (verifica _status interno).
 */
export async function hasPendingSync(): Promise<boolean> {
  return hasUnsyncedChanges({ database });
}
