import { api } from './api';
import { AlunoDto } from '../types/dtos';

export const alunosService = {
    obterPorTurma: async (turmaId: string): Promise<AlunoDto[]> => {
        const response = await api.get(`/alunos/turma/${turmaId}`);
        return response.data;
    },

    criar: async (payload: { nome: string; matricula?: string; turmaId: string }): Promise<AlunoDto> => {
        const response = await api.post('/alunos', payload);
        return response.data;
    },

    atualizar: async (id: string, payload: { id: string; nome: string; matricula?: string }): Promise<void> => {
        await api.put(`/alunos/${id}`, payload);
    },

    obterHistoricoPresencas: async (id: string): Promise<import('../types/dtos').HistoricoPresencaDto[]> => {
        const response = await api.get(`/alunos/${id}/historico-presencas`);
        return response.data;
    }
};
