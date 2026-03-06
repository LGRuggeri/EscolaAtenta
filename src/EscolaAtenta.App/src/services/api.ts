import axios from 'axios';
import * as SecureStore from 'expo-secure-store';
import { serverConfig } from './serverConfig';

const TOKEN_KEY = 'escolaatenta_jwt_token';

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

// Interceptador de Resposta: Trata erros globais (ex: Token Expirado 401)
api.interceptors.response.use(
    (response) => response,
    async (error) => {
        if (error.response && error.response.status === 401) {
            console.warn('Sessao expirada ou nao autorizada (401).');
        }
        return Promise.reject(error);
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
    }
};
