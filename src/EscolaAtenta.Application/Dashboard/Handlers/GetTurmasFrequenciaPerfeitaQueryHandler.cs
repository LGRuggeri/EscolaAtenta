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
        // SQLite suporta comparação de long (UtcTicks) em LINQ — evita carregar tudo em memória
        var inicioTicks = request.DataInicio.ToUniversalTime().Ticks;
        var fimTicks = request.DataFim.ToUniversalTime().Ticks;

        // Projeção direta no banco: conta chamadas por turma no período e verifica se há falta/atraso
        var resultado = await _context.Turmas
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Nome,
                QuantidadeChamadas = t.Chamadas
                    .Count(c => c.DataHora.UtcTicks >= inicioTicks && c.DataHora.UtcTicks <= fimTicks),
                TemFaltaOuAtraso = t.Chamadas
                    .Any(c => c.DataHora.UtcTicks >= inicioTicks && c.DataHora.UtcTicks <= fimTicks
                           && c.RegistrosPresenca.Any(rp =>
                               rp.Status == StatusPresenca.Falta ||
                               rp.Status == StatusPresenca.Atraso))
            })
            .Where(t => t.QuantidadeChamadas > 0 && !t.TemFaltaOuAtraso)
            .OrderByDescending(t => t.QuantidadeChamadas)
            .ThenBy(t => t.Nome)
            .ToListAsync(cancellationToken);

        return resultado.Select(t => new TurmaFrequenciaPerfeitaDto(t.Id, t.Nome, t.QuantidadeChamadas));
    }
}
