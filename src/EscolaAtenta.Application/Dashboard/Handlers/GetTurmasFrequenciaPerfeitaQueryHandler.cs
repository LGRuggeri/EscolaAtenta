using EscolaAtenta.Application.Dashboard.Dtos;
using EscolaAtenta.Application.Dashboard.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EscolaAtenta.Application.Dashboard.Handlers;

public class GetTurmasFrequenciaPerfeitaQueryHandler : IRequestHandler<GetTurmasFrequenciaPerfeitaQuery, IEnumerable<TurmaFrequenciaPerfeitaDto>>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTurmasFrequenciaPerfeitaQueryHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<TurmaFrequenciaPerfeitaDto>> Handle(GetTurmasFrequenciaPerfeitaQuery request, CancellationToken cancellationToken)
    {
        // IDOR: Administrador pode consultar qualquer turma; demais papéis precisam de vínculo
        HashSet<Guid>? turmasPermitidas = null;
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
        {
            turmasPermitidas = await _context.UsuarioTurmas
                .AsNoTracking()
                .Where(ut => ut.UsuarioId == usuarioId)
                .Select(ut => ut.TurmaId)
                .ToHashSetAsync(cancellationToken);
        }

        // O provider SQLite do EF Core não traduz projeções sobre coleções de navegação
        // que envolvam DateTimeOffset (ex: DataHora.UtcTicks). Para evitar a exceção,
        // filtramos as chamadas no banco pela data (DateTime) e materializamos os registros
        // de presença em memória, fazendo a agregação com LINQ-to-Objects.
        var inicio = request.DataInicio.Date;
        var fim = request.DataFim.Date;

        // Aplica o filtro de permissão diretamente na query do banco para evitar
        // carregar chamadas de turmas não autorizadas em memória.
        var query = _context.Chamadas
            .AsNoTracking()
            .Where(c => c.DataChamada >= inicio && c.DataChamada <= fim)
            .AsQueryable();

        if (turmasPermitidas is not null)
        {
            query = query.Where(c => turmasPermitidas.Contains(c.TurmaId));
        }

        var chamadasDoPeriodo = await query
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
