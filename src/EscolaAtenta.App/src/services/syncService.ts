import { Q } from '@nozbe/watermelondb';
import database from '../database';
import RegistroPresenca from '../database/models/RegistroPresenca';
import { api } from './api';

// ── Tipos do contrato com a API .NET ─────────────────────────────────────────

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

interface RegistroPresencaSyncDto {
  id: string;
  alunoId: string;
  turmaId: string;
  data: number;
  status: string;
}

interface SyncPushResult {
  registrosSincronizados: number;
  alertasGerados: number;
}

// ── Serviço de Sincronização ─────────────────────────────────────────────────

export const syncService = {
  /**
   * Busca todos os registros de presença "sujos" (sincronizado == false)
   * e retorna a quantidade pendente de envio.
   */
  async contarPendentes(): Promise<number> {
    const collection = database.get<RegistroPresenca>('registros_presenca');
    return collection.query(Q.where('sincronizado', false)).fetchCount();
  },

  /**
   * Motor principal de Push:
   * 1. Busca registros não-sincronizados no WatermelonDB
   * 2. Monta o payload delta no formato esperado pela API
   * 3. Envia POST /sync/push
   * 4. Marca cada registro como sincronizado localmente
   *
   * Retorna o resultado do backend ou null se não havia nada para sincronizar.
   */
  async push(): Promise<SyncPushResult | null> {
    const collection = database.get<RegistroPresenca>('registros_presenca');

    // 1. Busca registros pendentes
    const pendentes = await collection
      .query(Q.where('sincronizado', false))
      .fetch();

    if (pendentes.length === 0) {
      return null;
    }

    // 2. Monta o payload delta
    const created: RegistroPresencaSyncDto[] = pendentes.map((reg) => ({
      id: reg.id,
      alunoId: reg.alunoId,
      turmaId: reg.turmaId,
      data: reg.data.getTime(),
      status: reg.status,
    }));

    const payload: SyncPushPayload = {
      changes: {
        registrosPresenca: {
          created,
          updated: [],
          deleted: [],
        },
      },
      lastPulledAt: Date.now(),
    };

    // 3. Envia para a API
    const response = await api.post<SyncPushResult>('/sync/push', payload);

    // 4. Marca registros como sincronizados (batch write)
    await database.write(async () => {
      const batchOps = pendentes.map((reg) =>
        reg.prepareUpdate((r) => {
          r.sincronizado = true;
        })
      );
      await database.batch(...batchOps);
    });

    return response.data;
  },
};
