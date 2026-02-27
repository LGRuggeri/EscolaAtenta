using EscolaAtenta.Application.Dashboard.Dtos;
using MediatR;
using System;
using System.Collections.Generic;

namespace EscolaAtenta.Application.Dashboard.Queries;

public record GetTurmasFrequenciaPerfeitaQuery(
    DateTime DataInicio, 
    DateTime DataFim
) : IRequest<IEnumerable<TurmaFrequenciaPerfeitaDto>>;
