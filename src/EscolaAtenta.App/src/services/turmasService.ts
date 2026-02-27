import { api } from './api';
import { TurmaDto } from '../types/dtos';

export const turmasService = {
    obterTodas: async (): Promise<TurmaDto[]> => {
        const response = await api.get<TurmaDto[]>('/turmas');
        return response.data;
    },

    criar: async (data: Omit<TurmaDto, 'id'>): Promise<TurmaDto> => {
        const response = await api.post<TurmaDto>('/turmas', data);
        return response.data;
    },

    atualizar: async (id: string, data: Omit<TurmaDto, 'id'>): Promise<void> => {
        await api.put(`/turmas/${id}`, data);
    }
};
