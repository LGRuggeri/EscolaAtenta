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
        var turmasPerfeitas = await _context.Turmas
            .AsNoTracking()
            // Regra 1: Deve ter pelo menos uma chamada no período
            .Where(t => t.Chamadas.Any(c => c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim))
            // Regra 2: Não pode conter NENHUMA falta ou atraso injustificado nesse período
            .Where(t => !t.Chamadas
                .Where(c => c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim)
                .SelectMany(c => c.RegistrosPresenca)
                .Any(rp => rp.Status == StatusPresenca.Falta || rp.Status == StatusPresenca.Atraso))
            .Select(t => new TurmaFrequenciaPerfeitaDto(
                t.Id,
                t.Nome,
                t.Chamadas.Count(c => c.DataHora >= request.DataInicio && c.DataHora <= request.DataFim)
            ))
            // Ordenamos por turmas que tiveram mais aulas ministradas primeiro, depois nome
            .OrderByDescending(t => t.QuantidadeAulasMinistradas)
            .ThenBy(t => t.NomeTurma)
            .ToListAsync(cancellationToken);

        return turmasPerfeitas;
    }
}
