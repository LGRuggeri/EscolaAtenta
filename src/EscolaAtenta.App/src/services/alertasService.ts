import { api } from './api';
import { AlertaDto, AuditoriaAlertaDto } from '../types/dtos';

// ── Tipos de paginação ─────────────────────────────────────────────────────────

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export interface GetAlertasParams {
    apenasNaoResolvidos?: boolean;
    pageNumber?: number;
    pageSize?: number;
    tipo?: import('../types/enums').TipoAlerta;
    nivel?: import('../types/enums').NivelAlertaFalta;
}

/**
 * Parâmetros do endpoint de auditoria GET /api/v1/alertas/auditoria.
 * Todos os filtros são opcionais — ausentes = sem restrição.
 */
export interface GetAuditoriaParams {
    pageNumber?: number;
    pageSize?: number;
    nomeAluno?: string;
    tipo?: import('../types/enums').TipoAlerta;
    dataInicio?: string; // ISO 8601 — ex: "2026-01-01"
    dataFim?: string;    // ISO 8601 — ex: "2026-03-04"
    signal?: AbortSignal;
}

// ── Service ────────────────────────────────────────────────────────────────────

export const alertasService = {
    /**
     * Busca alertas paginados do backend.
     *
     * @param params.apenasNaoResolvidos - Filtra apenas alertas pendentes (default=true)
     * @param params.pageNumber - Página a buscar, 1-indexed (default=1)
     * @param params.pageSize - Itens por página (default=20, máx=100 limitado pelo backend)
     * @param params.tipo - Filtra pelo TipoAlerta
     * @param params.nivel - Subfiltro opcional. Efetivo apenas quando o tipo no BD for 'Evasao/Falta'
     *
     * Retorna PagedResult<AlertaDto> com hasNextPage para suporte a Infinite Scroll.
     */
    obterPaginados: async (params: GetAlertasParams = {}): Promise<PagedResult<AlertaDto>> => {
        const {
            apenasNaoResolvidos = true,
            pageNumber = 1,
            pageSize = 20,
            tipo,
            nivel,
        } = params;

        const response = await api.get<PagedResult<AlertaDto>>('/alertas', {
            params: { apenasNaoResolvidos, pageNumber, pageSize, tipo, nivel },
        });
        return response.data;
    },

    /**
     * Compatibilidade: busca a primeira página de alertas ativos.
     * @deprecated Prefira obterPaginados() para suporte a Infinite Scroll.
     */
    obterAtivos: async (): Promise<AlertaDto[]> => {
        const resultado = await alertasService.obterPaginados({ pageNumber: 1, pageSize: 50 });
        return resultado.items;
    },

    resolver: async (alertaId: string, justificativa: string): Promise<void> => {
        await api.patch(`/alertas/${alertaId}/resolver`, { justificativa });
    },

    /**
     * Busca auditoria paginada de alertas RESOLVIDOS via endpoint dedicado.
     *
     * Endpoint: GET /api/v1/alertas/auditoria
     * Acesso: Supervisao e Administrador (Monitor recebe 403)
     *
     * @param params.nomeAluno - Busca parcial LIKE no nome do aluno
     * @param params.tipo - "Evasao" | "Atraso"
     * @param params.dataInicio - Data inicial de resolução (ISO 8601)
     * @param params.dataFim - Data final de resolução (ISO 8601, inclui o dia inteiro)
     * @param params.pageNumber - Página 1-indexed (default=1)
     * @param params.pageSize - Itens por página (default=20, hard cap=100 no backend)
     */
    getAuditoriaAlertas: async (
        params: GetAuditoriaParams = {}
    ): Promise<PagedResult<AuditoriaAlertaDto>> => {
        const { pageNumber = 1, pageSize = 20, nomeAluno, tipo, dataInicio, dataFim, signal } = params;

        const response = await api.get<PagedResult<AuditoriaAlertaDto>>('/alertas/auditoria', {
            signal,
            params: {
                pageNumber,
                pageSize,
                // Envia apenas filtros com valor — parâmetros undefined são ignorados pelo axios
                ...(nomeAluno ? { nomeAluno } : {}),
                ...(tipo ? { tipo } : {}),
                ...(dataInicio ? { dataInicio } : {}),
                ...(dataFim ? { dataFim } : {}),
            },
        });
        return response.data;
    },
};

