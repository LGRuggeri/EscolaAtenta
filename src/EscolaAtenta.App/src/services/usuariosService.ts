import { api } from './api';
import { PapelUsuario } from '../types/enums';
import { PagedResult } from './alertasService';

// ── DTOs ─────────────────────────────────────────────────────────────────────

export interface CriarUsuarioCommand {
    nome: string;
    email: string;
    papel: PapelUsuario;
}

export interface UsuarioCriadoResult {
    id: string;
    email: string;
    senhaInicial: string;
}

export interface UsuarioDto {
    id: string;
    nome: string;
    email: string;
    papel: string;
    ativo: boolean;
}

export interface GetUsuariosParams {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    papel?: number;
}

export interface AtualizarUsuarioData {
    nome: string;
    papel: number;
}

// ── Service ──────────────────────────────────────────────────────────────────

export const usuariosService = {
    criarUsuario: async (data: CriarUsuarioCommand): Promise<UsuarioCriadoResult> => {
        const response = await api.post('/usuarios', data);
        return response.data;
    },

    getUsuarios: async (params: GetUsuariosParams = {}): Promise<PagedResult<UsuarioDto>> => {
        const { pageNumber = 1, pageSize = 20, searchTerm, papel } = params;

        const response = await api.get<PagedResult<UsuarioDto>>('/usuarios', {
            params: {
                pageNumber,
                pageSize,
                ...(searchTerm ? { searchTerm } : {}),
                ...(papel !== undefined ? { papel } : {}),
            },
        });
        return response.data;
    },

    getUsuarioById: async (id: string): Promise<UsuarioDto> => {
        const response = await api.get<UsuarioDto>(`/usuarios/${id}`);
        return response.data;
    },

    atualizarUsuario: async (id: string, data: AtualizarUsuarioData): Promise<void> => {
        await api.put(`/usuarios/${id}`, data);
    },

    alternarStatusUsuario: async (id: string): Promise<void> => {
        await api.patch(`/usuarios/${id}/status`);
    },
};
