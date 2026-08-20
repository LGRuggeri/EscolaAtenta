using EscolaAtenta.Application.Turmas.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Queries;

public record RelatorioTurmaQuery(
    Guid TurmaId,
    int AnoLetivo,
    int? PeriodoLetivo = null) : IRequest<RelatorioTurmaDto>;
