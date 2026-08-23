using EscolaAtenta.Application.Dashboard.Dtos;
using EscolaAtenta.Application.Dashboard.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EscolaAtenta.Application.Dashboard.Handlers;

public class GetTurmasFrequenciaPerfeitaQueryHandler : IRequestHandler<GetTurmasFrequenciaPerfeitaQuery, IEnumerable<TurmaFrequenciaPerfeitaDto>>
{
    private readonly AppDbContext _context;

    public GetTurmasFrequenciaPerfeitaQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TurmaFrequenciaPerfeitaDto>> Handle(GetTurmasFrequenciaPerfeitaQuery request, CancellationToken cancellationToken)
    {
        // Pré-filtra no banco usando DataChamada (DateTime), que é traduzível pelo provider SQLite.
        // Apenas os IDs das chamadas do período são carregados; depois as chamadas efetivas
        // e seus registros de presença são materializados em memória para a projeção final.
        var inicio = request.DataInicio.Date;
        var fim = request.DataFim.Date;

        var turmas = await _context.Turmas
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var chamadaIdsDoPeriodo = await _context.Chamadas
            .AsNoTracking()
            .Where(c => c.DataChamada >= inicio && c.DataChamada <= fim)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var chamadasDoPeriodo = await _context.Chamadas
            .AsNoTracking()
            .Where(c => chamadaIdsDoPeriodo.Contains(c.Id))
            .Include(c => c.RegistrosPresenca)
            .ToListAsync(cancellationToken);

        var resultado = chamadasDoPeriodo
            .Where(c => turmas.ContainsKey(c.TurmaId))
            .GroupBy(c => c.TurmaId)
            .Select(g => new
            {
                TurmaId = g.Key,
                QuantidadeChamadas = g.Count(),
                TemFaltaOuAtraso = g.Any(c => c.RegistrosPresenca.Any(rp =>
                    rp.Status == StatusPresenca.Falta ||
                    rp.Status == StatusPresenca.Atraso))
            })
            .Where(t => t.QuantidadeChamadas > 0 && !t.TemFaltaOuAtraso)
            .OrderByDescending(t => t.QuantidadeChamadas)
            .ThenBy(t => turmas[t.TurmaId].Nome)
            .Select(t => new TurmaFrequenciaPerfeitaDto(t.TurmaId, turmas[t.TurmaId].Nome, t.QuantidadeChamadas))
            .ToList();

        return resultado;
    }
}
