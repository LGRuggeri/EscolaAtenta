using EscolaAtenta.Application.Escola.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Escola.Queries;

public record ObterConfiguracaoEscolaQuery : IRequest<ConfiguracaoEscolaDto>;
