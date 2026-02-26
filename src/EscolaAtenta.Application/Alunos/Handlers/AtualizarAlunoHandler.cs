using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Infrastructure.Data;
using MediatR;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class AtualizarAlunoHandler : IRequestHandler<AtualizarAlunoCommand, Unit>
{
    private readonly AppDbContext _context;

    public AtualizarAlunoHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(AtualizarAlunoCommand request, CancellationToken cancellationToken)
    {
        var aluno = await _context.Alunos.FindAsync([request.Id], cancellationToken);

        if (aluno == null)
            return Unit.Value;

        aluno.Atualizar(request.Nome, request.Matricula);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
