using MediatR;

namespace EscolaAtenta.Application.Alunos.Commands;

public record AtualizarAlunoCommand(Guid Id, string Nome, string? Matricula) : IRequest<Unit>;
