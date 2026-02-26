using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Infrastructure.Data;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class AtualizarTurmaHandler : IRequestHandler<AtualizarTurmaCommand, Unit>
{
    private readonly AppDbContext _context;

    public AtualizarTurmaHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(AtualizarTurmaCommand request, CancellationToken cancellationToken)
    {
        var turma = await _context.Turmas.FindAsync([request.Id], cancellationToken);

        if (turma == null)
            return Unit.Value;

        turma.Atualizar(request.Nome, request.Turno, request.AnoLetivo);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
