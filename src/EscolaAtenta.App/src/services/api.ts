import axios from 'axios';
import * as SecureStore from 'expo-secure-store';

// IMPORTANTE: Para rodar no celular físico, substitua 'localhost' pelo IP da sua máquina na rede Wi-Fi.
// Exemplo: 'http://192.168.1.15:5114/api/v1'
const API_BASE_URL = 'http://192.168.3.27:5114/api/v1';
const TOKEN_KEY = 'escolaatenta_jwt_token';

export const api = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Interceptador de Requisição: Anexa o token JWT antes da chamada sair
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
            // Futuro: Implementar lógica de logout automático ou refresh token aqui
            console.warn('Sessão expirada ou não autorizada (401).');
        }
        return Promise.reject(error);
    }
);

// Funções utilitárias para gerenciar o token no cofre
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
