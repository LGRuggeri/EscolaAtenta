import { useState, useEffect, useCallback, useRef } from 'react';
import { AppState, AppStateStatus } from 'react-native';
import { syncWithServer, hasPendingSync } from '../services/sync/watermelondbSync';

interface SyncState {
  /** true enquanto um ciclo de sync está em andamento */
  isSyncing: boolean;
  /** true se existem registros locais aguardando push */
  temPendentes: boolean;
  /** mensagem de erro do último sync falhado (null = sem erro) */
  erro: string | null;
  /** timestamp do último sync bem-sucedido */
  ultimoSync: number | null;
}

/**
 * Hook que orquestra o ciclo de vida da sincronização offline-first.
 *
 * Agora usa a `synchronize()` nativa do WatermelonDB, que gerencia
 * automaticamente quais registros são sujos e limpa a flag interna
 * (_status) após push bem-sucedido.
 *
 * Gatilhos:
 * - Auto-sync quando o app retorna ao foreground.
 * - Polling periódico a cada 60s enquanto o app está ativo.
 * - `sincronizarAgora()` para disparo manual via UI.
 */
export function useSyncEngine() {
  const [state, setState] = useState<SyncState>({
    isSyncing: false,
    temPendentes: false,
    erro: null,
    ultimoSync: null,
  });

  const isMounted = useRef(true);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const verificarPendentes = useCallback(async () => {
    const pendentes = await hasPendingSync();
    if (isMounted.current) {
      setState((prev) => ({ ...prev, temPendentes: pendentes }));
    }
  }, []);

  const sincronizarAgora = useCallback(async () => {
    if (state.isSyncing) return; // Evita sync concorrente

    setState((prev) => ({ ...prev, isSyncing: true, erro: null }));

    try {
      await syncWithServer();

      if (isMounted.current) {
        setState((prev) => ({
          ...prev,
          isSyncing: false,
          ultimoSync: Date.now(),
        }));
      }

      await verificarPendentes();
    } catch (error: any) {
      // ── Tratamento de falha de rede ────────────────────────────────
      // Se o axios falhou (sem Wi-Fi, timeout, 5xx), o synchronize()
      // já abortou sem limpar os registros sujos. Eles serão reenviados
      // na próxima tentativa automaticamente.
      const mensagem =
        error?.response?.data?.detail ||
        error?.message ||
        'Sem conexão. Os registros serão enviados quando houver rede.';

      if (isMounted.current) {
        setState((prev) => ({
          ...prev,
          isSyncing: false,
          erro: mensagem,
        }));
      }
    }
  }, [state.isSyncing, verificarPendentes]);

  // ── Sync ao voltar para foreground ──────────────────────────────────────────
  useEffect(() => {
    const handleAppState = (nextState: AppStateStatus) => {
      if (nextState === 'active') {
        sincronizarAgora();
      }
    };

    const subscription = AppState.addEventListener('change', handleAppState);
    return () => subscription.remove();
  }, [sincronizarAgora]);

  // ── Polling periódico (60s) + sync inicial ─────────────────────────────────
  useEffect(() => {
    verificarPendentes();
    sincronizarAgora();

    intervalRef.current = setInterval(() => {
      sincronizarAgora();
    }, 60_000);

    return () => {
      isMounted.current = false;
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [sincronizarAgora, verificarPendentes]);

  return {
    ...state,
    sincronizarAgora,
  };
}
