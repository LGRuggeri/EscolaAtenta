namespace EscolaAtenta.Application.Alunos.DTOs;

public record HistoricoTurmaAlunoDto(
    Guid TurmaId,
    string NomeTurma,
    string Turno,
    int AnoLetivo,
    DateTime DataInicio,
    DateTime? DataFim,
    bool Ativa,
    string? Motivo);
