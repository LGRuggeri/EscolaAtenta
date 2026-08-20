using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Domain.Enums;
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
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastPulledAt).UtcDateTime
            : DateTime.MinValue;

        var isPrimeiroSync = lastPulledAt == 0;

        var changes = new SyncPullChanges();

        // ── TURMAS ───────────────────────────────────────────────────────────
        // Mapeamento GUID → ID local (para turmas criadas via push offline)
        // Quando o pull retorna uma turma que veio do push, usa o idExterno (ID local WatermelonDB)
        // como Id no payload, para que o celular atualize o registro existente ao invés de criar duplicata.
        var syncLogsTurmas = await _context.SyncLogs
            .AsNoTracking()
            .Where(s => s.TabelaOrigem == "turmas")
            .ToDictionaryAsync(s => s.EntidadeId, s => s.IdExterno, ct);

        string ResolverIdTurma(Guid guid) =>
            syncLogsTurmas.TryGetValue(guid, out var idLocal) ? idLocal : guid.ToString();

        // Carrega todas as turmas ativas em memória para filtragem.
        // Volume pequeno (app escolar) e evita UtcTicks que o SQLite provider não traduz.
        var todasTurmas = await _context.Turmas
            .AsNoTracking()
            .ToListAsync(ct);

        if (isPrimeiroSync)
        {
            changes.Turmas.Created = todasTurmas
                .Select(t => new TurmaSyncDto
                {
                    Id = ResolverIdTurma(t.Id),
                    Nome = t.Nome,
                    Turno = t.Turno,
                    AnoLetivo = t.AnoLetivo
                })
                .ToList();
        }
        else
        {
            // Delta: filtra em memória (SQLite EF Core não traduz DateTimeOffset.UtcTicks)
            changes.Turmas.Created = todasTurmas
                .Where(t => t.DataCriacao.UtcDateTime > sinceUtc)
                .Select(t => new TurmaSyncDto { Id = ResolverIdTurma(t.Id), Nome = t.Nome, Turno = t.Turno, AnoLetivo = t.AnoLetivo })
                .ToList();

            changes.Turmas.Updated = todasTurmas
                .Where(t => t.DataAtualizacao != null
                         && t.DataAtualizacao.Value.UtcDateTime > sinceUtc
                         && t.DataCriacao.UtcDateTime <= sinceUtc)
                .Select(t => new TurmaSyncDto { Id = ResolverIdTurma(t.Id), Nome = t.Nome, Turno = t.Turno, AnoLetivo = t.AnoLetivo })
                .ToList();

            // Soft-deleted: precisa de IgnoreQueryFilters, carrega separado
            var turmasExcluidas = await _context.Turmas
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => !t.Ativo)
                .ToListAsync(ct);

            changes.Turmas.Deleted = turmasExcluidas
                .Where(t => t.DataExclusao != null && t.DataExclusao.Value.UtcDateTime > sinceUtc)
                .Select(t => ResolverIdTurma(t.Id))
                .ToList();
        }

        // ── ALUNOS ───────────────────────────────────────────────────────────
        // Mesmo padrão: se o aluno veio do push offline, usa o ID local como Id no pull
        var syncLogsAlunos = await _context.SyncLogs
            .AsNoTracking()
            .Where(s => s.TabelaOrigem == "alunos")
            .ToDictionaryAsync(s => s.EntidadeId, s => s.IdExterno, ct);

        string ResolverIdAluno(Guid guid) =>
            syncLogsAlunos.TryGetValue(guid, out var idLocal) ? idLocal : guid.ToString();

        // TurmaId no payload do pull também precisa ser resolvido para o ID local (se veio de push)
        string ResolverTurmaId(Guid turmaGuid) =>
            syncLogsTurmas.TryGetValue(turmaGuid, out var idLocal) ? idLocal : turmaGuid.ToString();

        // Carrega todos os alunos ativos em memória para filtragem
        var todosAlunos = await _context.Alunos
            .AsNoTracking()
            .ToListAsync(ct);

        // ── Contadores trimestrais legados (compatibilidade app v3) ────────────────
        // Como ConfiguracaoEscola foi removida, deriva os totais do histórico de
        // frequência do trimestre corrente. Campos ainda são esperados pelo app
        // antigo para exibição na lista de alunos; valores zerados causariam
        // informação falsa silenciosa após o OTA.
        // Avaliação em memória: SQLite EF Core não traduz DateTimeOffset em
        // comparações com navegação (r.Chamada.DataHora).
        var (inicioTrimestre, fimTrimestre) = IntervaloTrimestreAtual(DateTime.Today);
        var contadoresTrimestre = (await _context.RegistrosPresenca
                .AsNoTracking()
                .Include(r => r.Chamada)
                .ToListAsync(ct))
            .Where(r => r.Chamada.DataHora >= inicioTrimestre && r.Chamada.DataHora <= fimTrimestre)
            .GroupBy(r => r.AlunoId)
            .Select(g => new
            {
                AlunoId = g.Key,
                Faltas = g.Count(r => r.Status == StatusPresenca.Falta || r.Status == StatusPresenca.Ausente),
                Atrasos = g.Count(r => r.Status == StatusPresenca.Atraso)
            })
            .ToDictionary(x => x.AlunoId);

        AlunoSyncDto MapAluno(Domain.Entities.Aluno a)
        {
            _ = contadoresTrimestre.TryGetValue(a.Id, out var contador);
            return new AlunoSyncDto
            {
                Id = ResolverIdAluno(a.Id),
                Nome = a.Nome,
                TurmaId = ResolverTurmaId(a.TurmaId),
                FaltasConsecutivasAtuais = a.FaltasConsecutivasAtuais,
                TotalFaltas = a.TotalFaltas,
                FaltasNoTrimestre = contador?.Faltas ?? 0,
                AtrasosNoTrimestre = contador?.Atrasos ?? 0
            };
        }

        static (DateTime Inicio, DateTime Fim) IntervaloTrimestreAtual(DateTime hoje)
        {
            var trimestre = hoje.Month switch
            {
                >= 1 and <= 3 => 1,
                >= 4 and <= 6 => 2,
                >= 7 and <= 9 => 3,
                _ => 4
            };

            return trimestre switch
            {
                1 => (new DateTime(hoje.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                      new DateTime(hoje.Year, 3, 31, 23, 59, 59, DateTimeKind.Utc)),
                2 => (new DateTime(hoje.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                      new DateTime(hoje.Year, 6, 30, 23, 59, 59, DateTimeKind.Utc)),
                3 => (new DateTime(hoje.Year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                      new DateTime(hoje.Year, 9, 30, 23, 59, 59, DateTimeKind.Utc)),
                _ => (new DateTime(hoje.Year, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                      new DateTime(hoje.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc))
            };
        }

        if (isPrimeiroSync)
        {
            changes.Alunos.Created = todosAlunos.Select(MapAluno).ToList();
        }
        else
        {
            var idsAlunoPushOffline = syncLogsAlunos.Keys.ToHashSet();

            var alunosCriados = todosAlunos
                .Where(a => a.DataCriacao.UtcDateTime > sinceUtc)
                .ToList();

            // Alunos genuinamente novos (não vieram de push offline)
            changes.Alunos.Created = alunosCriados
                .Where(a => !idsAlunoPushOffline.Contains(a.Id))
                .Select(MapAluno)
                .ToList();

            changes.Alunos.Updated = todosAlunos
                .Where(a => a.DataAtualizacao != null
                         && a.DataAtualizacao.Value.UtcDateTime > sinceUtc
                         && a.DataCriacao.UtcDateTime <= sinceUtc)
                .Select(MapAluno)
                .ToList();

            // Alunos criados via push offline devem ir em Updated (já existem localmente com ID local)
            changes.Alunos.Updated.AddRange(
                alunosCriados
                    .Where(a => idsAlunoPushOffline.Contains(a.Id))
                    .Select(MapAluno)
            );

            // Soft-deleted: precisa de IgnoreQueryFilters, carrega separado
            var alunosExcluidos = await _context.Alunos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => !a.Ativo)
                .ToListAsync(ct);

            changes.Alunos.Deleted = alunosExcluidos
                .Where(a => a.DataExclusao != null && a.DataExclusao.Value.UtcDateTime > sinceUtc)
                .Select(a => ResolverIdAluno(a.Id))
                .ToList();
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
