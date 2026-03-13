import { useEffect, useRef, useCallback } from 'react';
import { AppState, PanResponder } from 'react-native';

const INACTIVITY_TIMEOUT_MS = 3 * 60 * 1000; // 3 minutos

/**
 * Hook que monitora inatividade do usuário e executa logout automático.
 * Reseta o timer a cada toque na tela ou quando o app volta ao foreground.
 * Após 3 minutos sem interação, chama onLogout().
 */
export function useInactivityLogout(onLogout: () => void) {
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const onLogoutRef = useRef(onLogout);
    onLogoutRef.current = onLogout;

    const resetTimer = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(() => {
            onLogoutRef.current();
        }, INACTIVITY_TIMEOUT_MS);
    }, []);

    // PanResponder captura qualquer toque na tela sem interferir na UI
    const panResponder = useRef(
        PanResponder.create({
            onStartShouldSetPanResponderCapture: () => {
                resetTimer();
                return false; // Não captura o evento — apenas observa
            },
        })
    ).current;

    // Reseta o timer ao voltar para foreground
    useEffect(() => {
        const sub = AppState.addEventListener('change', state => {
            if (state === 'active') resetTimer();
        });
        return () => sub.remove();
    }, [resetTimer]);

    // Inicia o timer ao montar
    useEffect(() => {
        resetTimer();
        return () => {
            if (timerRef.current) clearTimeout(timerRef.current);
        };
    }, [resetTimer]);

    return panResponder.panHandlers;
}
