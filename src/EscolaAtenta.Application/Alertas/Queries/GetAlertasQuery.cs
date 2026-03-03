using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Alertas.Queries;

/// <summary>
/// Query para listar alertas com suporte a paginação server-side.
///
/// Parâmetros:
/// - ApenasNaoResolvidos: filtra somente alertas pendentes (default=true)
/// - PageNumber: página solicitada, 1-indexed (default=1)
/// - PageSize: itens por página (default=20, max=100)
///
/// O TotalCount retornado pelo PagedResult é calculado por um COUNT separado
/// antes do Skip/Take, garantindo que o front-end saiba o volume real sem
/// carregar todos os registros na memória.
/// </summary>
public record GetAlertasQuery(
    bool ApenasNaoResolvidos = true,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<AlertaEvasaoDto>>
{
    public TipoAlerta? Tipo { get; set; }
    public NivelAlertaFalta? Nivel { get; set; }
}
