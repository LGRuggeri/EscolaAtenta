using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Common;
using MediatR;

namespace EscolaAtenta.Application.Alertas.Queries;

/// <summary>
/// Query de auditoria de alertas de evasão resolvidos com filtros e paginação obrigatória.
///
/// Esta é uma query de Read Model — não modifica estado, otimizada para leitura.
///
/// Filtros opcionais:
/// - NomeAluno: LIKE parcial (case-insensitive no PostgreSQL)
/// - DataInicio / DataFim: intervalo de DataResolucao
///
/// Paginação: PageNumber (1-indexed), PageSize (default=20, hard cap=100 no Handler).
/// </summary>
public record GetAuditoriaAlertasQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<AuditoriaAlertaDto>>
{
    public string? NomeAluno { get; init; }
    public DateTime? DataInicio { get; init; }
    public DateTime? DataFim { get; init; }
}
