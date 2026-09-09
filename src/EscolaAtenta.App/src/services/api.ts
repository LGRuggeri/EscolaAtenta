import axios from 'axios';
import * as SecureStore from 'expo-secure-store';
import { serverConfig } from './serverConfig';
import { sessionScope } from './sessionScope';

const TOKEN_KEY = 'escolaatenta_jwt_token';
const REFRESH_TOKEN_KEY = 'escolaatenta_refresh_token';
const TOKEN_SERVER_KEY = 'escolaatenta_token_server';
let onSessionInvalid: (() => void) | undefined;
let onPasswordRequired: (() => void) | undefined;
export function setSessionHandlers(invalid?: () => void, password?: () => void): void {
    onSessionInvalid = invalid;
    onPasswordRequired = password;
}
export const api = axios.create({ timeout: 30000, headers: { 'Content-Type': 'application/json' } });

export async function loadServerUrl(): Promise<boolean> {
    const url = await serverConfig.getUrl();
    api.defaults.baseURL = url ? `${url}/api/v1` : undefined;
    return !!url;
}

api.interceptors.request.use(async (config) => {
    const scoped = config as typeof config & { _session?: number; _finish?: () => void };
    scoped._session ??= sessionScope.generation;
    sessionScope.assert(scoped._session);
    const token = await SecureStore.getItemAsync(TOKEN_KEY);
    const tokenServer = await SecureStore.getItemAsync(TOKEN_SERVER_KEY);
    sessionScope.assert(scoped._session);
    delete config.headers.Authorization;
    if (token && tokenServer === config.baseURL && !config.url?.includes('/auth/login')) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    sessionScope.track(new Promise<void>(resolve => { scoped._finish = resolve; }));
    return config;
});

let refresh: Promise<string> | null = null;
api.interceptors.response.use(response => {
    (response.config as any)._finish?.();
    sessionScope.assert((response.config as any)._session);
    return response;
}, async error => {
    const request = error.config;
    request?._finish?.();
    if (!request) throw error;
    sessionScope.assert(request._session);
    if (error.response?.status === 403 && error.response?.data?.code === 'troca_senha_obrigatoria') {
        onPasswordRequired?.();
    }
    if (error.response?.status !== 401 || request._retry || request.url?.includes('/auth/')) throw error;
    request._retry = true;
    const generation = request._session;
    if (!refresh) {
        refresh = sessionScope.track((async () => {
            const refreshToken = await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
            sessionScope.assert(generation);
            if (!refreshToken) throw new Error('Sem refresh token');
            const response = await api.post('/auth/refresh', { refreshToken });
            sessionScope.assert(generation);
            await authStorage.saveToken(response.data.token);
            await authStorage.saveRefreshToken(response.data.refreshToken);
            if (response.data.deveAlterarSenha === true) onPasswordRequired?.();
            return response.data.token as string;
        })());
    }
    const currentRefresh = refresh;
    try {
        await currentRefresh;
        sessionScope.assert(generation);
        return api(request);
    } catch (failure) {
        if (generation === sessionScope.generation) {
            await authStorage.removeToken();
            onSessionInvalid?.();
        }
        throw failure;
    } finally {
        if (refresh === currentRefresh) refresh = null;
    }
});

export const authStorage = {
    saveToken: async (token: string) => {
        await SecureStore.setItemAsync(TOKEN_SERVER_KEY, api.defaults.baseURL ?? '');
        await SecureStore.setItemAsync(TOKEN_KEY, token);
    },
    getToken: async () => {
        const server = await SecureStore.getItemAsync(TOKEN_SERVER_KEY);
        // Tokens legados sem origem exigem login para não serem enviados a outro servidor.
        return server === api.defaults.baseURL ? SecureStore.getItemAsync(TOKEN_KEY) : null;
    },
    removeToken: async () => {
        delete api.defaults.headers.common.Authorization;
        await SecureStore.deleteItemAsync(TOKEN_KEY);
        await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
        await SecureStore.deleteItemAsync(TOKEN_SERVER_KEY);
    },
    saveRefreshToken: async (token: string) => {
        await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
    },
};
