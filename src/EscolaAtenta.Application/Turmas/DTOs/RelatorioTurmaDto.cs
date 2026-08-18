namespace EscolaAtenta.Application.Turmas.DTOs;

public record RelatorioTurmaDto(
    Guid TurmaId,
    string NomeTurma,
    string Turno,
    int AnoLetivo,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    IReadOnlyList<RelatorioTurmaAlunoDto> Alunos,
    RelatorioTurmaResumoDto Resumo);

public record RelatorioTurmaAlunoDto(
    Guid AlunoId,
    string NomeAluno,
    string? Matricula,
    int Presentes,
    int Faltas,
    int FaltasJustificadas,
    int Atrasos,
    double PercentualPresenca);

public record RelatorioTurmaResumoDto(
    int TotalAlunos,
    int TotalPresentes,
    int TotalFaltas,
    int TotalFaltasJustificadas,
    int TotalAtrasos,
    double PercentualPresencaTurma);
