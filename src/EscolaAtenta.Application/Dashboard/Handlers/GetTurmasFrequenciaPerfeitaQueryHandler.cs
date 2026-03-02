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
        // Buscamos apenas os IDs e Nomes das Turmas que satisfazem as regras
        var turmasValidas = await _context.Turmas
            .AsNoTracking()
            // Regra 1: Deve ter pelo menos uma chamada no período
            .Where(t => t.Chamadas.Any(c => c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim))
            // Regra 2: Não pode conter NENHUMA falta ou atraso injustificado nesse período
            .Where(t => !t.Chamadas
                .Any(c => c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim &&
                     c.RegistrosPresenca.Any(rp => rp.Status == StatusPresenca.Falta || rp.Status == StatusPresenca.Atraso)))
            .Select(t => new { t.Id, t.Nome })
            .ToListAsync(cancellationToken);

        if (!turmasValidas.Any())
            return Enumerable.Empty<TurmaFrequenciaPerfeitaDto>();

        var turmaIds = turmasValidas.Select(t => t.Id).ToList();

        // Count das aulas ministradas por turma no período
        var contagemChamadas = await _context.Chamadas
            .AsNoTracking()
            .Where(c => turmaIds.Contains(c.TurmaId) && c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim)
            .GroupBy(c => c.TurmaId)
            .Select(g => new { TurmaId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.TurmaId, g => g.Quantidade, cancellationToken);

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
