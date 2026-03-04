import { PapelUsuario, NivelAlertaFalta, TipoAlerta } from './enums';

export interface UsuarioLogado {
    id: string;
    nome: string;
    email: string;
    papel: PapelUsuario;
}

export interface LoginResult {
    token: string;
    email: string;
    papel: string;
    expiresAt: string;
}

export interface TurmaDto {
    id: string;
    nome: string;
    anoLetivo: number;
    turno: string;
}

export interface AlunoDto {
    id: string;
    nome: string;
    matricula: string;
    turmaId: string;
    faltasConsecutivasAtuais: number;
    faltasNoTrimestre: number;
    totalFaltas: number;
    atrasosNoTrimestre: number;
}

export interface RegistroPresencaPayload {
    alunoId: string;
    status: import('./enums').StatusPresenca;
    justificativa?: string;
}

export interface RealizarChamadaPayload {
    turmaId: string;
    responsavelId: string;
    alunos: RegistroPresencaPayload[];
}

export interface AlertaDto {
    id: string;
    nomeAluno: string;
    nomeTurma: string;
    nivel: NivelAlertaFalta;
    descricao: string;
    dataAlerta: string;
    resolvido: boolean;
    observacaoResolucao?: string;
    tituloAmigavel: string;
    mensagemAcao: string;
    /** Discriminador de tipo enviado pelo backend. Usar TipoAlerta enum — não comparar strings diretamente. */
    tipo: TipoAlerta;
    resolvidoPorNome?: string;
    dataResolucao?: string;
    justificativaResolucao?: string;
}

export interface HistoricoPresencaDto {
    dataDaChamada: string;
    status: string;
    justificativa: string | null;
}

export interface TurmaFrequenciaPerfeitaDto {
    turmaId: string;
    nomeTurma: string;
    quantidadeAulasMinistradas: number;
}

/**
 * DTO do endpoint GET /api/v1/alertas/auditoria.
 * Representa um alerta já resolvido, com informações de responsabilidade e motivo.
 *
 * - tipoAlerta: "Evasao" | "Atraso" (string — não enum numérico)
 * - nivelAlerta: "Aviso" | "Intermediario" | "Vermelho" | "Preto" (string)
 * - resolvidoPor: e-mail do usuário que resolveu, ou "Sistema"
 */
export interface AuditoriaAlertaDto {
    id: string;
    nomeAluno: string;
    tipoAlerta: string;        // "Evasao" | "Atraso"
    dataResolucao: string;     // ISO 8601 UTC
    resolvidoPor: string;      // e-mail ou "Sistema"
    motivoResolucao: string;
    nivelAlerta: string;       // "Aviso" | "Intermediario" | "Vermelho" | "Preto"
    dataAlerta: string;        // ISO 8601 UTC
}

