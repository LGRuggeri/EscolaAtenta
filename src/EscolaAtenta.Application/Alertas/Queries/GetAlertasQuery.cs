using EscolaAtenta.Application.Alertas.Dtos;
using MediatR;

namespace EscolaAtenta.Application.Alertas.Queries;

public record GetAlertasQuery(bool ApenasNaoResolvidos = true) : IRequest<IEnumerable<AlertaEvasaoDto>>;
