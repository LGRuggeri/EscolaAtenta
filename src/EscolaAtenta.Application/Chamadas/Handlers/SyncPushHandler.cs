using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

/// <summary>
/// Processa o push de registros de presença criados/atualizados offline pelo WatermelonDB.
///
/// Fluxo Created:
/// 1. Converte Data (Unix ms) → DateTimeOffset UTC.
/// 2. Agrupa por (TurmaId + Dia) → uma Chamada por turma por dia.
/// 3. Adiciona RegistroPresenca via domínio + atualiza contadores do Aluno.
///
/// Fluxo Updated:
/// 1. Localiza o RegistroPresenca no PostgreSQL via SyncLog (IdExterno → EntidadeId).
/// 2. Aplica AlterarStatus() com o novo status vindo do celular.
///
/// Transação única: um SaveChangesAsync() no final processa tudo atomicamente.
/// </summary>
public class SyncPushHandler : IRequestHandler<SyncPushCommand, SyncPushResult>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SyncPushHandler> _logger;
    private readonly ISqliteWriteLockProvider _lockProvider;

    public SyncPushHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<SyncPushHandler> logger,
        ISqliteWriteLockProvider lockProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _lockProvider = lockProvider;
    }

    public async Task<SyncPushResult> Handle(SyncPushCommand request, CancellationToken cancellationToken)
    {
        var turmasCriadas = request.Changes.Turmas.Created;
        var alunosCriados = request.Changes.Alunos.Created;
        var created = request.Changes.RegistrosPresenca.Created;
        var updated = request.Changes.RegistrosPresenca.Updated;

        if (turmasCriadas.Count == 0 && alunosCriados.Count == 0 && created.Count == 0 && updated.Count == 0)
            return new SyncPushResult(0, 0);

        // ── Segurança: Responsável extraído do JWT, nunca do cliente ─────────
        var responsavelId = _currentUser.EstaAutenticado
            && Guid.TryParse(_currentUser.UsuarioId, out var parsedUserId)
            ? parsedUserId
            : throw new DomainException("Usuário não autenticado. Sync requer autenticação.");

        int totalSincronizados = 0;
        int alertasGerados = 0;

        await _lockProvider.WaitAsync(cancellationToken);
        try
        {
            // ── TURMAS CRIADAS OFFLINE ────────────────────────────────────────────
            if (turmasCriadas.Count > 0)
            {
                totalSincronizados += await ProcessarTurmasCriadas(turmasCriadas, cancellationToken);
                // Persiste turmas e SyncLogs antes de processar alunos,
                // para que o lookup por IdExterno no SyncLog funcione corretamente.
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ── ALUNOS CRIADOS OFFLINE ────────────────────────────────────────────
            if (alunosCriados.Count > 0)
            {
                totalSincronizados += await ProcessarAlunosCriados(alunosCriados, cancellationToken);
                // Persiste alunos e SyncLogs antes de processar presenças,
                // para que o lookup por IdExterno no SyncLog funcione corretamente.
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ── CREATED ──────────────────────────────────────────────────────────
            if (created.Count > 0)
            {
                var (criados, alertas) = await ProcessarCreated(created, responsavelId, cancellationToken);
                totalSincronizados += criados;
                alertasGerados += alertas;
            }

            // ── UPDATED ──────────────────────────────────────────────────────────
            if (updated.Count > 0)
            {
                totalSincronizados += await ProcessarUpdated(updated, cancellationToken);
            }

            // ── Persistência atômica (domain events despachados no SaveChanges) ──
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _lockProvider.Release();
        }

        _logger.LogInformation(
            "[SYNC-PUSH] Concluído — Turmas={Turmas} Alunos={Alunos} Created={Created} Updated={Updated} Alertas={Alertas} Responsavel={User}",
            turmasCriadas.Count, alunosCriados.Count, created.Count, updated.Count, alertasGerados, responsavelId);

        return new SyncPushResult(totalSincronizados, alertasGerados);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TURMAS CRIADAS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarTurmasCriadas(
        List<TurmaOfflineSyncDto> turmas,
        CancellationToken ct)
    {
        // Idempotência: ignorar turmas já sincronizadas
        var idsExternos = turmas.Select(t => t.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var novas = turmas.Where(t => !idsJaSincronizados.Contains(t.Id)).ToList();
        if (novas.Count == 0) return 0;

        int criados = 0;

        foreach (var dto in novas)
        {
            var turno = string.IsNullOrWhiteSpace(dto.Turno) ? "Matutino" : dto.Turno;
            var anoLetivo = dto.AnoLetivo > 0 ? dto.AnoLetivo : DateTime.UtcNow.Year;

            var turma = new Turma(Guid.NewGuid(), dto.Nome, turno, anoLetivo);
            _context.Turmas.Add(turma);

            _context.SyncLogs.Add(new SyncLog
            {
                Id = Guid.NewGuid(),
                IdExterno = dto.Id,
                EntidadeId = turma.Id,
                TabelaOrigem = "turmas",
                SincronizadoEm = DateTimeOffset.UtcNow
            });

            criados++;
        }

        _logger.LogInformation("[SYNC-TURMA] {Count} turma(s) criada(s) offline sincronizadas.", criados);
        return criados;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATED: Novos registros de presença gerados offline
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<(int Criados, int Alertas)> ProcessarCreated(
        List<RegistroPresencaSyncDto> registros,
        Guid responsavelId,
        CancellationToken ct)
    {
        // 1. Idempotência: filtrar IDs já sincronizados
        var idsExternos = registros.Select(r => r.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var registrosNovos = registros
            .Where(r => !idsJaSincronizados.Contains(r.Id))
            .ToList();

        if (registrosNovos.Count == 0)
            return (0, 0);

        // 2. Resolver IDs externos (WatermelonDB local) → GUIDs reais via SyncLog
        //    AlunoId e TurmaId podem ser IDs locais (ex: "NnshRE3qD8uI4cdW") ou GUIDs reais.
        var todosIdsExternos = registrosNovos
            .SelectMany(r => new[] { r.AlunoId, r.TurmaId })
            .Where(id => !Guid.TryParse(id, out _))
            .Distinct()
            .ToList();

        var syncLogMap = todosIdsExternos.Count > 0
            ? await _context.SyncLogs
                .Where(s => todosIdsExternos.Contains(s.IdExterno))
                .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct)
            : new Dictionary<string, Guid>();

        Guid ResolveGuid(string id)
        {
            if (Guid.TryParse(id, out var guid)) return guid;
            return syncLogMap.TryGetValue(id, out var resolved) ? resolved : Guid.Empty;
        }

        // 3. Agrupar por (TurmaId resolvido + Dia) → uma Chamada por turma por dia
        var grupos = registrosNovos
            .GroupBy(r => new
            {
                TurmaGuid = ResolveGuid(r.TurmaId),
                Dia = ConvertTimestamp(r.Data).Date
            })
            .ToList();

        // 4. Validar turmas existentes
        var turmaGuids = grupos.Select(g => g.Key.TurmaGuid).Where(g => g != Guid.Empty).Distinct().ToList();
        var turmasExistentes = await _context.Turmas
            .Where(t => turmaGuids.Contains(t.Id))
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        // 5. Carregar alunos necessários (para atualizar contadores)
        var todosAlunoGuids = registrosNovos
            .Select(r => ResolveGuid(r.AlunoId))
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        var alunosDb = await _context.Alunos
            .Where(a => todosAlunoGuids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        int criados = 0;
        int alertas = 0;

        foreach (var grupo in grupos)
        {
            if (grupo.Key.TurmaGuid == Guid.Empty || !turmasExistentes.Contains(grupo.Key.TurmaGuid))
            {
                _logger.LogWarning("[SYNC] Turma '{TurmaId}' não encontrada. Ignorando {Count} registros.",
                    grupo.First().TurmaId, grupo.Count());
                continue;
            }

            // Converte o timestamp do primeiro registro do grupo para a DataHora da Chamada
            var primeiroTimestamp = grupo.Min(r => r.Data);
            var dataHoraChamada = ConvertTimestamp(primeiroTimestamp);

            // Cria a Chamada via domínio (preserva invariantes de duplicidade)
            var chamada = new Chamada(
                id: Guid.NewGuid(),
                dataHora: dataHoraChamada,
                turmaId: grupo.Key.TurmaGuid,
                responsavelId: responsavelId
            );

            _context.Chamadas.Add(chamada);

            foreach (var dto in grupo)
            {
                var alunoGuid = ResolveGuid(dto.AlunoId);
                if (alunoGuid == Guid.Empty || !alunosDb.TryGetValue(alunoGuid, out var aluno))
                {
                    _logger.LogWarning("[SYNC] Aluno '{AlunoId}' não encontrado. Registro ignorado.", dto.AlunoId);
                    continue;
                }

                var status = ParseStatus(dto.Status);

                // Registra na Chamada → cria o RegistroPresenca via domínio
                var registro = chamada.RegistrarPresenca(aluno.Id, status);

                // Atualiza contadores do aluno (dispara domain events se threshold atingido)
                var dataPresenca = ConvertTimestamp(dto.Data).UtcDateTime;
                aluno.RegistrarPresenca(status, dataPresenca);

                if (aluno.DomainEvents.Count > 0)
                    alertas++;

                // Registra mapeamento WatermelonDB ID → PostgreSQL ID para futuros Updates
                _context.SyncLogs.Add(new SyncLog
                {
                    Id = Guid.NewGuid(),
                    IdExterno = dto.Id,
                    EntidadeId = registro.Id,
                    TabelaOrigem = "registros_presenca",
                    SincronizadoEm = DateTimeOffset.UtcNow
                });

                criados++;
            }
        }

        return (criados, alertas);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATED: Registros com status corrigido offline após sync anterior
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarUpdated(
        List<RegistroPresencaSyncDto> registros,
        CancellationToken ct)
    {
        // 1. Busca os mapeamentos IdExterno → EntidadeId (PostgreSQL)
        var idsExternos = registros.Select(r => r.Id).ToList();
        var mapeamentos = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct);

        // 2. Carrega os RegistroPresenca do PostgreSQL para atualizar
        var entidadeIds = mapeamentos.Values.ToList();
        var registrosDb = await _context.RegistrosPresenca
            .Where(rp => entidadeIds.Contains(rp.Id))
            .ToDictionaryAsync(rp => rp.Id, ct);

        int atualizados = 0;

        foreach (var dto in registros)
        {
            if (!mapeamentos.TryGetValue(dto.Id, out var entidadeId))
            {
                _logger.LogWarning(
                    "[SYNC-UPDATE] SyncLog não encontrado para IdExterno={IdExterno}. Registro nunca foi sincronizado?",
                    dto.Id);
                continue;
            }

            if (!registrosDb.TryGetValue(entidadeId, out var registroPresenca))
            {
                _logger.LogWarning(
                    "[SYNC-UPDATE] RegistroPresenca {EntidadeId} não encontrado no banco.",
                    entidadeId);
                continue;
            }

            var novoStatus = ParseStatus(dto.Status);

            // Atualiza via método de domínio (valida transição de status)
            try
            {
                registroPresenca.AlterarStatus(novoStatus);
                atualizados++;
            }
            catch (DomainException ex)
            {
                // Status já é o mesmo — não é erro, apenas skip
                _logger.LogDebug("[SYNC-UPDATE] Skip: {Mensagem}", ex.Message);
            }
        }

        return atualizados;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ALUNOS CRIADOS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarAlunosCriados(
        List<AlunoOfflineSyncDto> alunos,
        CancellationToken ct)
    {
        // Idempotência: ignorar alunos já sincronizados
        var idsExternos = alunos.Select(a => a.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var novos = alunos.Where(a => !idsJaSincronizados.Contains(a.Id)).ToList();
        if (novos.Count == 0) return 0;

        // Pré-carrega SyncLogs de turmas locais (IDs que não são Guid) — evita N+1
        var turmaIdsLocais = novos
            .Where(a => !Guid.TryParse(a.TurmaId, out _))
            .Select(a => a.TurmaId)
            .Distinct()
            .ToList();

        var syncLogsTurma = turmaIdsLocais.Count > 0
            ? await _context.SyncLogs
                .Where(s => turmaIdsLocais.Contains(s.IdExterno))
                .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct)
            : new Dictionary<string, Guid>();

        // Coleta todos os turmaGuids candidatos para validar existência em batch
        var turmaGuidsCandidatos = new HashSet<Guid>();
        foreach (var dto in novos)
        {
            if (Guid.TryParse(dto.TurmaId, out var g))
                turmaGuidsCandidatos.Add(g);
            else if (syncLogsTurma.TryGetValue(dto.TurmaId, out var mapped))
                turmaGuidsCandidatos.Add(mapped);
        }

        var turmasExistentes = turmaGuidsCandidatos.Count > 0
            ? await _context.Turmas
                .Where(t => turmaGuidsCandidatos.Contains(t.Id))
                .Select(t => t.Id)
                .ToHashSetAsync(ct)
            : new HashSet<Guid>();

        int criados = 0;

        foreach (var dto in novos)
        {
            // TurmaId pode ser um Guid real (sync'd) ou um ID local (WatermelonDB)
            Guid turmaGuid;
            if (!Guid.TryParse(dto.TurmaId, out turmaGuid))
            {
                if (!syncLogsTurma.TryGetValue(dto.TurmaId, out turmaGuid))
                {
                    _logger.LogWarning("[SYNC-ALUNO] TurmaId {TurmaId} não encontrado. Aluno {Nome} ignorado.", dto.TurmaId, dto.Nome);
                    continue;
                }
            }

            if (!turmasExistentes.Contains(turmaGuid))
            {
                _logger.LogWarning("[SYNC-ALUNO] Turma {TurmaId} não existe no servidor. Aluno {Nome} ignorado.", turmaGuid, dto.Nome);
                continue;
            }

            var aluno = new Aluno(Guid.NewGuid(), dto.Nome, null, turmaGuid);
            _context.Alunos.Add(aluno);

            _context.SyncLogs.Add(new SyncLog
            {
                Id = Guid.NewGuid(),
                IdExterno = dto.Id,
                EntidadeId = aluno.Id,
                TabelaOrigem = "alunos",
                SincronizadoEm = DateTimeOffset.UtcNow
            });

            criados++;
        }

        _logger.LogInformation("[SYNC-ALUNO] {Count} aluno(s) criado(s) offline sincronizados.", criados);
        return criados;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converte Unix Timestamp em milissegundos (SQLite/WatermelonDB) → DateTimeOffset UTC.
    /// Exemplo: 1709654400000 → 2024-03-05T16:00:00+00:00
    /// </summary>
    private static DateTimeOffset ConvertTimestamp(long unixMilliseconds)
        => DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);

    private static StatusPresenca ParseStatus(string status) => status switch
    {
        "Presente" => StatusPresenca.Presente,
        "Falta" => StatusPresenca.Falta,
        "Atraso" => StatusPresenca.Atraso,
        "FaltaJustificada" => StatusPresenca.FaltaJustificada,
        _ => throw new DomainException(
            $"Status de presença inválido: '{status}'. Valores aceitos: Presente, Falta, Atraso, FaltaJustificada.")
    };
}
