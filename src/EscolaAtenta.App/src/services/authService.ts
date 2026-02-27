import { api } from './api';
import { LoginResult } from '../types/dtos';

export const authService = {
    login: async (email: string, senha: string): Promise<LoginResult> => {
        const response = await api.post<LoginResult>('/auth/login', { email, senha });
        return response.data;
    }
};
