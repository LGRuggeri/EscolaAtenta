using EscolaAtenta.Application.Alunos.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Alunos.Queries;

public record ObterHistoricoTurmasAlunoQuery(string AlunoIdOuExterno) : IRequest<IEnumerable<HistoricoTurmaAlunoDto>>;
