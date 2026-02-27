using EscolaAtenta.Application.Alunos.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace EscolaAtenta.Application.Alunos.Queries;

public record GetHistoricoPresencasAlunoQuery(Guid AlunoId) : IRequest<IEnumerable<HistoricoPresencaDto>>;
