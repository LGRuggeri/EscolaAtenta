import * as SecureStore from 'expo-secure-store';

const SERVER_URL_KEY = 'escolaatenta_server_url';

export const serverConfig = {
    getUrl: async (): Promise<string | null> => {
        return await SecureStore.getItemAsync(SERVER_URL_KEY);
    },

    saveUrl: async (url: string): Promise<void> => {
        const normalized = url.replace(/\/+$/, '');
        await SecureStore.setItemAsync(SERVER_URL_KEY, normalized);
    },

    testConnection: async (url: string): Promise<{ ok: boolean; message: string }> => {
        try {
            const normalized = url.replace(/\/+$/, '');
            const controller = new AbortController();
            const timeout = setTimeout(() => controller.abort(), 5000);

            const response = await fetch(`${normalized}/health`, {
                signal: controller.signal,
            });
            clearTimeout(timeout);

            if (response.ok) {
                return { ok: true, message: 'Servidor encontrado!' };
            }
            return { ok: false, message: `Servidor respondeu com status ${response.status}` };
        } catch (error) {
            if (error instanceof Error && error.name === 'AbortError') {
                return { ok: false, message: 'Timeout: servidor nao respondeu em 5 segundos.' };
            }
            return { ok: false, message: 'Nao foi possivel conectar. Verifique o IP e a porta.' };
        }
    },
};
