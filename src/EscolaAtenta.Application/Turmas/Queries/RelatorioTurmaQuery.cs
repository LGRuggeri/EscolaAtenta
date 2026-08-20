using EscolaAtenta.Application.Turmas.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Queries;

public record RelatorioTurmaQuery(
    Guid TurmaId,
    DateTime DataInicio,
    DateTime DataFim) : IRequest<RelatorioTurmaDto>;
