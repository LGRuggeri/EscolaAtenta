namespace EscolaAtenta.Application.Turmas.DTOs;

public record MigrarTurmaResultadoDto(
    int QuantidadeTransferida,
    int QuantidadeIgnorada,
    IReadOnlyList<string> Erros);
