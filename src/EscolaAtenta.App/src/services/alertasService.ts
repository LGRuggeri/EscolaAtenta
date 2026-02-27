import { api } from './api';
import { AlertaDto } from '../types/dtos';

export const alertasService = {
    obterAtivos: async (): Promise<AlertaDto[]> => {
        // API C# configurada em `AlertasController.cs` na rota base: /alertas
        const response = await api.get<AlertaDto[]>('/alertas');
        return response.data;
    },

    resolver: async (alertaId: string, tratativa: string): Promise<void> => {
        await api.patch(`/alertas/${alertaId}/resolver`, { tratativa });
    }
};
