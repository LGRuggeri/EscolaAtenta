using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class GetAlunosPorTurmaQueryHandler : IRequestHandler<GetAlunosPorTurmaQuery, IReadOnlyList<AlunoDto>>
{
    private readonly AppDbContext _context;

    public GetAlunosPorTurmaQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AlunoDto>> Handle(GetAlunosPorTurmaQuery request, CancellationToken cancellationToken)
    {
        var alunos = await _context.Alunos
            .AsNoTracking()
            .Where(a => a.TurmaId == request.TurmaId)
            .OrderBy(a => a.Nome)
            .Select(a => new AlunoDto(
                a.Id,
                a.Nome,
                a.Matricula,
                a.TurmaId,
                a.FaltasConsecutivasAtuais,
                a.TotalFaltas))
            .ToListAsync(cancellationToken);

        return alunos;
    }
}
