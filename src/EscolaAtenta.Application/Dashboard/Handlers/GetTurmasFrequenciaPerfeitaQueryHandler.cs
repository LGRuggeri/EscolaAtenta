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
        // SQLite não suporta comparação de DateTimeOffset em LINQ — carrega em memória e filtra
        var dataInicio = request.DataInicio.ToUniversalTime();
        var dataFim = request.DataFim.ToUniversalTime();

        var todasTurmas = await _context.Turmas
            .AsNoTracking()
            .Include(t => t.Chamadas)
                .ThenInclude(c => c.RegistrosPresenca)
            .ToListAsync(cancellationToken);

        var turmasValidas = todasTurmas
            .Where(t => t.Chamadas.Any(c => c.DataHora.UtcDateTime >= dataInicio && c.DataHora.UtcDateTime <= dataFim))
            .Where(t => !t.Chamadas
                .Any(c => c.DataHora.UtcDateTime >= dataInicio && c.DataHora.UtcDateTime <= dataFim &&
                     c.RegistrosPresenca.Any(rp => rp.Status == StatusPresenca.Falta || rp.Status == StatusPresenca.Atraso)))
            .Select(t => new { t.Id, t.Nome })
            .ToList();

        if (!turmasValidas.Any())
            return Enumerable.Empty<TurmaFrequenciaPerfeitaDto>();

        var turmaIds = turmasValidas.Select(t => t.Id).ToList();

        var todasChamadas = await _context.Chamadas
            .AsNoTracking()
            .Where(c => turmaIds.Contains(c.TurmaId))
            .ToListAsync(cancellationToken);

        var contagemChamadas = todasChamadas
            .Where(c => c.DataHora.UtcDateTime >= dataInicio && c.DataHora.UtcDateTime <= dataFim)
            .GroupBy(c => c.TurmaId)
            .ToDictionary(g => g.Key, g => g.Count());

        var turmasPerfeitas = turmasValidas
            .Select(t => new TurmaFrequenciaPerfeitaDto(
                t.Id,
                t.Nome,
                contagemChamadas.ContainsKey(t.Id) ? contagemChamadas[t.Id] : 0
            ))
            .OrderByDescending(t => t.QuantidadeAulasMinistradas)
            .ThenBy(t => t.NomeTurma)
            .ToList();

        return turmasPerfeitas;
    }
}
