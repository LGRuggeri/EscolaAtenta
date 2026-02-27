using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

public class GetAlertasHandler : IRequestHandler<GetAlertasQuery, IEnumerable<AlertaEvasaoDto>>
{
    private readonly AppDbContext _context;

    public GetAlertasHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AlertaEvasaoDto>> Handle(GetAlertasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AlertasEvasao.AsQueryable();

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        var alertas = await query
            .OrderByDescending(a => a.DataAlerta)
            .ToListAsync(cancellationToken);

        return alertas.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.AlunoId,
            a.TurmaId,
            a.Nivel,
            a.Descricao,
            a.DataAlerta.UtcDateTime,
            a.Resolvido,
            a.ObservacaoResolucao
        ));
    }
}
