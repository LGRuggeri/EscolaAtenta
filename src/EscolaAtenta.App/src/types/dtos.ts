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
    deveAlterarSenha?: boolean;
    refreshToken?: string;
}

export interface TurmaDto {
    id: string;
    serverId?: string;
    nome: string;
    anoLetivo: number;
    turno: string;
}

export interface AlunoDto {
    id: string;
    serverId?: string;
    nome: string;
    matricula: string;
    turmaId: string;
    faltasConsecutivasAtuais: number;
    totalFaltas: number;
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
    /** Data da chamada em ISO 8601 (ex: "2026-01-15T00:00:00.000Z"). Se omitido, usa a data/hora atual. */
    data?: string;
}

export interface ChamadaPorDiaDto {
    chamadaId: string;
    dataHora: string;
    responsavelId: string;
    podeEditar: boolean;
    registros: RegistroPorDiaDto[];
}

export interface RegistroPorDiaDto {
    alunoId: string;
    nomeAluno: string;
    /** Status da presença como string (Presente, Falta, Atraso, FaltaJustificada). */
    status: string;
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
 * - tipoAlerta: "Evasao" (string — não enum numérico)
 * - nivelAlerta: "Aviso" | "Intermediario" | "Vermelho" | "Preto" (string)
 * - resolvidoPor: e-mail do usuário que resolveu, ou "Sistema"
 */
export interface AuditoriaAlertaDto {
    id: string;
    nomeAluno: string;
    tipoAlerta: string;        // "Evasao"
    dataResolucao: string;     // ISO 8601 UTC
    resolvidoPor: string;      // e-mail ou "Sistema"
    motivoResolucao: string;
    nivelAlerta: string;       // "Aviso" | "Intermediario" | "Vermelho" | "Preto"
    dataAlerta: string;        // ISO 8601 UTC
}

export interface RelatorioTurmaAlunoDto {
    alunoId: string;
    nomeAluno: string;
    matricula: string | null;
    presentes: number;
    faltas: number;
    faltasJustificadas: number;
    atrasos: number;
    percentualPresenca: number;
}

export interface RelatorioTurmaResumoDto {
    totalAlunos: number;
    totalPresentes: number;
    totalFaltas: number;
    totalFaltasJustificadas: number;
    totalAtrasos: number;
    percentualPresencaTurma: number;
}

export interface RelatorioTurmaDto {
    turmaId: string;
    nomeTurma: string;
    turno: string;
    anoLetivo: number;
    periodoInicio: string;
    periodoFim: string;
    alunos: RelatorioTurmaAlunoDto[];
    resumo: RelatorioTurmaResumoDto;
}

export interface MigrarTurmaResultadoDto {
    quantidadeTransferida: number;
    quantidadeIgnorada: number;
    erros: string[];
}

