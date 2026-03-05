using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

/// <summary>
/// Monta o delta de Turmas e Alunos no formato WatermelonDB Sync Protocol.
///
/// Lógica de Delta (baseada nos campos de auditoria do EntityBase):
/// - Created: registros com DataCriacao > lastPulledAt (novos desde último sync)
/// - Updated: registros com DataAtualizacao > lastPulledAt E DataCriacao ≤ lastPulledAt
/// - Deleted: registros soft-deleted com DataExclusao > lastPulledAt (IDs apenas)
///
/// Primeiro login (lastPulledAt = 0): tudo ativo vai em Created.
/// </summary>
public class SyncPullHandler : IRequestHandler<SyncPullQuery, SyncPullResult>
{
    private readonly AppDbContext _context;
    private readonly ILogger<SyncPullHandler> _logger;

    public SyncPullHandler(AppDbContext context, ILogger<SyncPullHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SyncPullResult> Handle(SyncPullQuery request, CancellationToken ct)
    {
        var lastPulledAt = request.LastPulledAt ?? 0;
        var sinceUtc = lastPulledAt > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastPulledAt)
            : DateTimeOffset.MinValue;

        var isPrimeiroSync = lastPulledAt == 0;

        var changes = new SyncPullChanges();

        // ── TURMAS ───────────────────────────────────────────────────────────
        if (isPrimeiroSync)
        {
            // Primeiro sync: todas as turmas ativas vão em Created
            var todasTurmas = await _context.Turmas
                .AsNoTracking()
                .Select(t => new TurmaSyncDto
                {
                    Id = t.Id.ToString(),
                    Nome = t.Nome,
                    Turno = t.Turno
                })
                .ToListAsync(ct);

            changes.Turmas.Created = todasTurmas;
        }
        else
        {
            // Delta: Created = novas desde lastPulledAt
            changes.Turmas.Created = await _context.Turmas
                .AsNoTracking()
                .Where(t => t.DataCriacao > sinceUtc)
                .Select(t => new TurmaSyncDto
                {
                    Id = t.Id.ToString(),
                    Nome = t.Nome,
                    Turno = t.Turno
                })
                .ToListAsync(ct);

            // Delta: Updated = modificadas (existiam antes, atualizadas depois)
            changes.Turmas.Updated = await _context.Turmas
                .AsNoTracking()
                .Where(t => t.DataAtualizacao != null
                         && t.DataAtualizacao > sinceUtc
                         && t.DataCriacao <= sinceUtc)
                .Select(t => new TurmaSyncDto
                {
                    Id = t.Id.ToString(),
                    Nome = t.Nome,
                    Turno = t.Turno
                })
                .ToListAsync(ct);

            // Delta: Deleted = soft-deleted desde lastPulledAt
            changes.Turmas.Deleted = await _context.Turmas
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => !t.Ativo && t.DataExclusao != null && t.DataExclusao > sinceUtc)
                .Select(t => t.Id.ToString())
                .ToListAsync(ct);
        }

        // ── ALUNOS ───────────────────────────────────────────────────────────
        if (isPrimeiroSync)
        {
            var todosAlunos = await _context.Alunos
                .AsNoTracking()
                .Select(a => new AlunoSyncDto
                {
                    Id = a.Id.ToString(),
                    Nome = a.Nome,
                    TurmaId = a.TurmaId.ToString()
                })
                .ToListAsync(ct);

            changes.Alunos.Created = todosAlunos;
        }
        else
        {
            changes.Alunos.Created = await _context.Alunos
                .AsNoTracking()
                .Where(a => a.DataCriacao > sinceUtc)
                .Select(a => new AlunoSyncDto
                {
                    Id = a.Id.ToString(),
                    Nome = a.Nome,
                    TurmaId = a.TurmaId.ToString()
                })
                .ToListAsync(ct);

            changes.Alunos.Updated = await _context.Alunos
                .AsNoTracking()
                .Where(a => a.DataAtualizacao != null
                         && a.DataAtualizacao > sinceUtc
                         && a.DataCriacao <= sinceUtc)
                .Select(a => new AlunoSyncDto
                {
                    Id = a.Id.ToString(),
                    Nome = a.Nome,
                    TurmaId = a.TurmaId.ToString()
                })
                .ToListAsync(ct);

            changes.Alunos.Deleted = await _context.Alunos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => !a.Ativo && a.DataExclusao != null && a.DataExclusao > sinceUtc)
                .Select(a => a.Id.ToString())
                .ToListAsync(ct);
        }

        // Timestamp do servidor para o próximo pull
        var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _logger.LogInformation(
            "[SYNC-PULL] Delta montado — Turmas(C={TC} U={TU} D={TD}) Alunos(C={AC} U={AU} D={AD}) Since={Since}",
            changes.Turmas.Created.Count, changes.Turmas.Updated.Count, changes.Turmas.Deleted.Count,
            changes.Alunos.Created.Count, changes.Alunos.Updated.Count, changes.Alunos.Deleted.Count,
            isPrimeiroSync ? "PRIMEIRO_SYNC" : sinceUtc.ToString("o"));

        return new SyncPullResult
        {
            Changes = changes,
            Timestamp = serverTimestamp
        };
    }
}
