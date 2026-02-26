using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class GetTurmasQueryHandler : IRequestHandler<GetTurmasQuery, IReadOnlyList<TurmaDto>>
{
    private readonly AppDbContext _context;

    public GetTurmasQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TurmaDto>> Handle(GetTurmasQuery request, CancellationToken cancellationToken)
    {
        var turmas = await _context.Turmas
            .AsNoTracking()
            .OrderBy(t => t.Nome)
            .Select(t => new TurmaDto(t.Id, t.Nome, t.Turno, t.AnoLetivo))
            .ToListAsync(cancellationToken);

        return turmas;
    }
}
