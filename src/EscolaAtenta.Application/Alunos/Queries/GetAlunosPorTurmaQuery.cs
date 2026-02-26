using EscolaAtenta.Application.Alunos.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Alunos.Queries;

public record GetAlunosPorTurmaQuery(Guid TurmaId) : IRequest<IReadOnlyList<AlunoDto>>;
