import { api } from './api';
import { RealizarChamadaPayload } from '../types/dtos';

export const chamadasService = {
    realizarChamada: async (payload: RealizarChamadaPayload): Promise<void> => {
        await api.post('/chamadas/realizar', payload);
    }
};
