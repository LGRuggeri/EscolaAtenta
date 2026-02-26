using MediatR;

namespace EscolaAtenta.Application.Turmas.Commands;

public record AtualizarTurmaCommand(Guid Id, string Nome, string Turno, int AnoLetivo) : IRequest<Unit>;
