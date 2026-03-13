import axios from 'axios';
import * as SecureStore from 'expo-secure-store';
import { serverConfig } from './serverConfig';

const TOKEN_KEY = 'escolaatenta_jwt_token';
const REFRESH_TOKEN_KEY = 'escolaatenta_refresh_token';

export const api = axios.create({
    headers: {
        'Content-Type': 'application/json',
    },
});

/**
 * Carrega a URL do servidor salva e atualiza o baseURL do axios.
 * Deve ser chamado na inicializacao do app e apos salvar nova configuracao.
 */
export async function loadServerUrl(): Promise<boolean> {
    const url = await serverConfig.getUrl();
    if (url) {
        api.defaults.baseURL = `${url}/api/v1`;
        return true;
    }
    return false;
}

// Interceptador de Requisicao: Anexa o token JWT antes da chamada sair
api.interceptors.request.use(
    async (config) => {
        try {
            const token = await SecureStore.getItemAsync(TOKEN_KEY);
            if (token && config.headers) {
                config.headers.Authorization = `Bearer ${token}`;
            }
        } catch (error) {
            console.error('Erro ao recuperar o token do SecureStore', error);
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

// Interceptador de Resposta: renova JWT silenciosamente via Refresh Token
// Se o servidor retornar 401 e houver refresh token válido, tenta renovar uma vez.
let isRefreshing = false;
let refreshSubscribers: ((token: string) => void)[] = [];

api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;

        // Evita loop em endpoints de auth e requisições já repetidas
        if (
            error.response?.status !== 401 ||
            originalRequest._retry ||
            originalRequest.url?.includes('/auth/')
        ) {
            return Promise.reject(error);
        }

        if (isRefreshing) {
            // Aguarda o refresh em andamento e reenvia com o novo token
            return new Promise((resolve) => {
                refreshSubscribers.push((token: string) => {
                    originalRequest.headers.Authorization = `Bearer ${token}`;
                    resolve(api(originalRequest));
                });
            });
        }

        originalRequest._retry = true;
        isRefreshing = true;

        try {
            const refreshToken = await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
            if (!refreshToken) throw new Error('Sem refresh token');

            const response = await api.post('/auth/refresh', { refreshToken });
            const { token, refreshToken: novoRefresh } = response.data;

            await SecureStore.setItemAsync(TOKEN_KEY, token);
            await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, novoRefresh);

            api.defaults.headers.common.Authorization = `Bearer ${token}`;
            refreshSubscribers.forEach(cb => cb(token));
            refreshSubscribers = [];

            originalRequest.headers.Authorization = `Bearer ${token}`;
            return api(originalRequest);
        } catch {
            console.warn('[Auth] Refresh token inválido — sessão encerrada.');
            await SecureStore.deleteItemAsync(TOKEN_KEY);
            await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
            refreshSubscribers = [];
            return Promise.reject(error);
        } finally {
            isRefreshing = false;
        }
    }
);

// Funcoes utilitarias para gerenciar o token no cofre
export const authStorage = {
    saveToken: async (token: string) => {
        await SecureStore.setItemAsync(TOKEN_KEY, token);
    },
    getToken: async () => {
        return await SecureStore.getItemAsync(TOKEN_KEY);
    },
    removeToken: async () => {
        await SecureStore.deleteItemAsync(TOKEN_KEY);
        await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
    },
    saveRefreshToken: async (token: string) => {
        await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
    },
};
