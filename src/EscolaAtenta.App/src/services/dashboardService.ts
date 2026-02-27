import { api } from './api';
import { TurmaFrequenciaPerfeitaDto } from '../types/dtos';

export const dashboardService = {
    obterTurmasFrequenciaPerfeita: async (dataInicio: string, dataFim: string): Promise<TurmaFrequenciaPerfeitaDto[]> => {
        const response = await api.get('/dashboard/turmas-frequencia-perfeita', {
            params: { dataInicio, dataFim }
        });
        return response.data;
    }
};
