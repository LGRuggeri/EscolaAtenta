using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Escola.Commands;

public record AtualizarConfiguracaoEscolaCommand(TipoPeriodoLetivo TipoPeriodoLetivo) : IRequest;
