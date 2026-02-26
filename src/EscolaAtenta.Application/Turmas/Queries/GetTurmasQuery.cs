using EscolaAtenta.Application.Turmas.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Queries;

public record GetTurmasQuery : IRequest<IReadOnlyList<TurmaDto>>;
