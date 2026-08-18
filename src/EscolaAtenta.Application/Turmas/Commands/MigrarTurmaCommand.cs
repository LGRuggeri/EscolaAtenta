using EscolaAtenta.Application.Turmas.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Commands;

public record MigrarTurmaCommand(
    Guid TurmaOrigemId,
    Guid TurmaDestinoId,
    DateTime DataTransferencia,
    string? Motivo) : IRequest<MigrarTurmaResultadoDto>;
