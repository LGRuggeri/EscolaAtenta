import { api } from './api';
import { ChamadaPorDiaDto, RealizarChamadaPayload } from '../types/dtos';

export const chamadasService = {
    realizarChamada: async (payload: RealizarChamadaPayload): Promise<void> => {
        await api.post('/chamadas/realizar', payload);
    },

    obterChamadaPorDia: async (turmaId: string, data: Date): Promise<ChamadaPorDiaDto | null> => {
        try {
            // Normaliza para meia-noite UTC do mesmo dia calendário,
            // evitando divergência de fuso entre celular e servidor.
            const dataUtc = new Date(Date.UTC(data.getFullYear(), data.getMonth(), data.getDate()));
            const response = await api.get<ChamadaPorDiaDto>(`/chamadas/turma/${turmaId}/dia/${dataUtc.toISOString()}`);
            return response.data;
        } catch (error: any) {
            if (error.response?.status === 404) {
                return null;
            }
            throw error;
        }
    }
};
