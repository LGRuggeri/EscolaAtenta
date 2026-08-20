using MediatR;

namespace EscolaAtenta.Application.Alunos.Commands;

public record TransferirAlunoCommand(
    Guid AlunoId,
    Guid NovaTurmaId,
    DateTime DataTransferencia,
    string? Motivo) : IRequest;
