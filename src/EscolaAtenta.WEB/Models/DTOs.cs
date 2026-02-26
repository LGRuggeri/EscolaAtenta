namespace EscolaAtenta.WEB.Models;

public enum StatusPresenca
{
    Presente = 0,
    Falta = 1,
    FaltaJustificada = 2,
    Ausente = 3
}

public record TurmaDto(Guid Id, string Nome, string Turno, int AnoLetivo);
public record CriarTurmaRequest(string Nome, string Turno, int AnoLetivo);

public record AlunoDto(Guid Id, string Nome, string Matricula, Guid TurmaId, int FaltasConsecutivasAtuais, int TotalFaltas);
public record CriarAlunoRequest(string Nome, string? Matricula, Guid TurmaId);

public record RegistroAlunoDto(Guid AlunoId, StatusPresenca Status);
public record RealizarChamadaRequest(Guid TurmaId, Guid ResponsavelId, List<RegistroAlunoDto> Alunos);
public record RealizarChamadaResult(Guid ChamadaId, int AlertasGerados);

public record AlunoComFaltasDto(
    Guid Id,
    string Nome,
    string Matricula,
    Guid TurmaId,
    string NomeTurma,
    int FaltasConsecutivasAtuais,
    int TotalFaltas,
    string NivelAlerta
);

// --- Usuários ---
public enum PapelUsuarioDto
{
    Monitor = 1,
    Supervisao = 2,
    Administrador = 3
}

public record CriarUsuarioRequest(string Nome, string Email, PapelUsuarioDto Papel);
public record UsuarioCriadoResponse(Guid Id, string Email, string SenhaInicial);
