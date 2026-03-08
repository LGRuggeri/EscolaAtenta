import { useState, useEffect, useCallback, useRef } from 'react';
import { AppState, AppStateStatus } from 'react-native';
import { syncWithServer, hasPendingSync } from '../services/sync/watermelondbSync';

interface SyncState {
  isSyncing: boolean;
  temPendentes: boolean;
  erro: string | null;
  ultimoSync: number | null;
}

/**
 * Hook que orquestra o ciclo de vida da sincronização offline-first.
 *
 * Gatilhos:
 * - Sync automático ao montar o componente (primeiro login/abertura).
 * - Sync ao voltar para foreground.
 * - Polling periódico a cada 60s.
 * - `sincronizarAgora()` para disparo manual via UI.
 */
export function useSyncEngine() {
  const [state, setState] = useState<SyncState>({
    isSyncing: false,
    temPendentes: false,
    erro: null,
    ultimoSync: null,
  });

  // Ref para evitar stale closure — permite verificar isSyncing sem recria o callback
  const isSyncingRef = useRef(false);
  const isMounted = useRef(true);

  const verificarPendentes = useCallback(async () => {
    try {
      const pendentes = await hasPendingSync();
      if (isMounted.current) {
        setState((prev) => ({ ...prev, temPendentes: pendentes }));
      }
    } catch {
      // ignorar erros de verificação
    }
  }, []);

  const sincronizarAgora = useCallback(async () => {
    if (isSyncingRef.current) return;

    isSyncingRef.current = true;
    if (isMounted.current) {
      setState((prev) => ({ ...prev, isSyncing: true, erro: null }));
    }

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
    } finally {
      isSyncingRef.current = false;
    }
  }, [verificarPendentes]);

  // Sync ao voltar para foreground
  useEffect(() => {
    const handleAppState = (nextState: AppStateStatus) => {
      if (nextState === 'active') {
        sincronizarAgora();
      }
    };

    const subscription = AppState.addEventListener('change', handleAppState);
    return () => subscription.remove();
  }, [sincronizarAgora]);

  // Sync inicial + polling periódico (60s)
  useEffect(() => {
    isMounted.current = true;

    verificarPendentes();
    sincronizarAgora();

    const interval = setInterval(() => {
      sincronizarAgora();
    }, 60_000);

    return () => {
      isMounted.current = false;
      clearInterval(interval);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Intencionalmente sem dependências — executa só uma vez ao montar

  return {
    ...state,
    sincronizarAgora,
  };
}
