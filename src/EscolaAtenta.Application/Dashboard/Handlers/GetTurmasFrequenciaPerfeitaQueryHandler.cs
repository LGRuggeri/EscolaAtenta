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
        // O provider SQLite do EF Core não traduz projeções sobre coleções de navegação
        // que envolvam DateTimeOffset (ex: DataHora.UtcTicks). Para evitar a exceção,
        // filtramos as chamadas no banco pela data (DateTime) e materializamos os registros
        // de presença em memória, fazendo a agregação com LINQ-to-Objects.
        var inicio = request.DataInicio.Date;
        var fim = request.DataFim.Date;

        var chamadasDoPeriodo = await _context.Chamadas
            .AsNoTracking()
            .Where(c => c.DataChamada >= inicio && c.DataChamada <= fim)
            .Include(c => c.RegistrosPresenca)
            .ToListAsync(cancellationToken);

        var turmasIdsComChamadas = chamadasDoPeriodo
            .Select(c => c.TurmaId)
            .Distinct()
            .ToList();

        var turmas = await _context.Turmas
            .AsNoTracking()
            .Where(t => turmasIdsComChamadas.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var statusInadmissiveis = new HashSet<StatusPresenca>
        {
            StatusPresenca.Falta,
            StatusPresenca.FaltaJustificada,
            StatusPresenca.Ausente,
            StatusPresenca.Atraso
        };

        var resultado = chamadasDoPeriodo
            .GroupBy(c => c.TurmaId)
            .Select(g => new
            {
                TurmaId = g.Key,
                QuantidadeChamadas = g.Count(),
                TemInadmissivel = g.Any(c => c.RegistrosPresenca.Any(rp => statusInadmissiveis.Contains(rp.Status)))
            })
            .Where(t => !t.TemInadmissivel)
            .OrderByDescending(t => t.QuantidadeChamadas)
            .ThenBy(t => turmas[t.TurmaId].Nome)
            .Select(t => new TurmaFrequenciaPerfeitaDto(t.TurmaId, turmas[t.TurmaId].Nome, t.QuantidadeChamadas))
            .ToList();

        return resultado;
    }
}
