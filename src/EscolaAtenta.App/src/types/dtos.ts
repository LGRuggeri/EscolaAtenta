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
