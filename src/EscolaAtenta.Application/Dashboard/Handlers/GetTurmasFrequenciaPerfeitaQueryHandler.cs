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
        // O provider SQLite do EF Core não traduz UtcTicks em projeção sobre coleção de navegação.
        // Como o volume de dados de uma escola é pequeno, carregamos as chamadas do período em memória
        // e fazemos a projeção final em LINQ-to-Objects, garantindo compatibilidade com SQLite.
        var inicioUtc = request.DataInicio.ToUniversalTime();
        var fimUtc = request.DataFim.ToUniversalTime();

        var turmas = await _context.Turmas
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        // Carrega todas as chamadas (volume reduzido) e filtra o período em memória,
        // evitando tradução de DateTimeOffset no provider SQLite.
        var chamadasDoPeriodo = (await _context.Chamadas
            .AsNoTracking()
            .Include(c => c.RegistrosPresenca)
            .ToListAsync(cancellationToken))
            .Where(c => c.DataHora >= inicioUtc && c.DataHora <= fimUtc)
            .ToList();

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
