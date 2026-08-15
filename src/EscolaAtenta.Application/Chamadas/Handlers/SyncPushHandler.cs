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
/// 3. Se já existir Chamada para o dia:
///    - Dentro de 7 dias da criação: atualiza os status dos RegistroPresenca existentes.
///    - Fora de 7 dias: ignora o grupo (log warning).
/// 4. Se não existir: cria nova Chamada e adiciona os registros.
///
/// Fluxo Updated:
/// 1. Localiza o RegistroPresenca no banco via SyncLog (IdExterno → EntidadeId).
/// 2. Verifica se a Chamada pai ainda está dentro do prazo de 7 dias.
/// 3. Se dentro: aplica AlterarStatus(). Se fora: ignora.
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
            return new SyncPushResult(0, 0, []);

        // ── Segurança: Responsável extraído do JWT, nunca do cliente ─────────
        var responsavelId = _currentUser.EstaAutenticado
            && Guid.TryParse(_currentUser.UsuarioId, out var parsedUserId)
            ? parsedUserId
            : throw new DomainException("Usuário não autenticado. Sync requer autenticação.");

        int totalSincronizados = 0;
        int alertasGerados = 0;
        var alunosAfetados = new HashSet<Guid>();
        var rejeicoes = new List<SyncRejeicao>();

        // ── IDOR: verifica se o usuário tem permissão para cada turma envolvida ─
        await ValidarOwnershipAsync(request, rejeicoes, cancellationToken);

        await _lockProvider.WaitAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // ── TURMAS CRIADAS OFFLINE ────────────────────────────────────────────
            if (turmasCriadas.Count > 0)
            {
                totalSincronizados += await ProcessarTurmasCriadas(turmasCriadas, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ── ALUNOS CRIADOS OFFLINE ────────────────────────────────────────────
            if (alunosCriados.Count > 0)
            {
                totalSincronizados += await ProcessarAlunosCriados(alunosCriados, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ── CREATED ──────────────────────────────────────────────────────────
            if (created.Count > 0)
            {
                var (criados, alertas, afetados) = await ProcessarCreated(created, responsavelId, rejeicoes, cancellationToken);
                totalSincronizados += criados;
                alertasGerados += alertas;
                foreach (var id in afetados) alunosAfetados.Add(id);
            }

            // ── UPDATED ──────────────────────────────────────────────────────────
            if (updated.Count > 0)
            {
                var (atualizados, afetados) = await ProcessarUpdated(updated, rejeicoes, cancellationToken);
                totalSincronizados += atualizados;
                foreach (var id in afetados) alunosAfetados.Add(id);
            }

            // ── Recálculo de estatísticas dos alunos afetados ────────────────────
            if (alunosAfetados.Count > 0)
            {
                await RecalcularEstatisticasDosAlunos(alunosAfetados, cancellationToken);

                // Conta alertas gerados pela recalculagem (antes do SaveChanges limpar os eventos)
                var alunosDb = await _context.Alunos
                    .Where(a => alunosAfetados.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                foreach (var alunoId in alunosAfetados)
                {
                    if (alunosDb.TryGetValue(alunoId, out var aluno) && aluno.DomainEvents.Count > 0)
                    {
                        alertasGerados++;
                    }
                }
            }

            // Se houver rejeições (prazo expirado ou permissão), descarta toda a transação.
            // Isso evita que registros válidos sejam commitados e depois fiquem presos em retry infinito
            // junto com registros rejeitados no WatermelonDB.
            if (rejeicoes.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    "[SYNC-PUSH] Rejeitado — {Rejeicoes} registro(s) rejeitado(s). Transação revertida.",
                    rejeicoes.Count);
                return new SyncPushResult(0, 0, rejeicoes);
            }

            // ── Persistência atômica (domain events despachados no SaveChanges) ──
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _lockProvider.Release();
        }

        _logger.LogInformation(
            "[SYNC-PUSH] Concluído — Turmas={Turmas} Alunos={Alunos} Created={Created} Updated={Updated} Alertas={Alertas} Rejeicoes={Rejeicoes} Responsavel={User}",
            turmasCriadas.Count, alunosCriados.Count, created.Count, updated.Count, alertasGerados, rejeicoes.Count, responsavelId);

        return new SyncPushResult(totalSincronizados, alertasGerados, rejeicoes);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TURMAS CRIADAS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarTurmasCriadas(
        List<TurmaOfflineSyncDto> turmas,
        CancellationToken ct)
    {
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

    private async Task<(int Criados, int Alertas, HashSet<Guid> Afetados)> ProcessarCreated(
        List<RegistroPresencaSyncDto> registros,
        Guid responsavelId,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        var idsExternos = registros.Select(r => r.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var registrosNovos = registros
            .Where(r => !idsJaSincronizados.Contains(r.Id))
            .ToList();

        if (registrosNovos.Count == 0)
            return (0, 0, []);

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

        var grupos = registrosNovos
            .GroupBy(r => new
            {
                TurmaGuid = ResolveGuid(r.TurmaId),
                Dia = ConvertTimestamp(r.Data).Date
            })
            .ToList();

        var turmaGuids = grupos.Select(g => g.Key.TurmaGuid).Where(g => g != Guid.Empty).Distinct().ToList();
        var turmasExistentes = await _context.Turmas
            .Where(t => turmaGuids.Contains(t.Id))
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        var todosAlunoGuids = registrosNovos
            .Select(r => ResolveGuid(r.AlunoId))
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        var alunosDb = await _context.Alunos
            .Where(a => todosAlunoGuids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        // Carrega chamadas existentes para os grupos (turma + dia)
        // Filtragem por data é feita em memória para compatibilidade com SQLite/DateTimeOffset.
        var chamadasExistentes = await _context.Chamadas
            .Include(c => c.RegistrosPresenca)
            .Where(c => turmaGuids.Contains(c.TurmaId))
            .ToListAsync(ct);

        var datasDosGrupos = grupos.Select(g => g.Key.Dia).ToHashSet();
        var chamadasPorChave = chamadasExistentes
            .Where(c => datasDosGrupos.Contains(c.DataChamada))
            .GroupBy(c => (c.TurmaId, c.DataChamada))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.DataCriacao).ThenBy(c => c.Id).First());

        int criados = 0;
        int alertas = 0;
        var afetados = new HashSet<Guid>();

        foreach (var grupo in grupos)
        {
            if (grupo.Key.TurmaGuid == Guid.Empty || !turmasExistentes.Contains(grupo.Key.TurmaGuid))
            {
                _logger.LogWarning("[SYNC] Turma '{TurmaId}' não encontrada. Ignorando {Count} registros.",
                    grupo.First().TurmaId, grupo.Count());
                continue;
            }

            var dataHoraChamada = ConvertTimestamp(grupo.Min(r => r.Data));
            var chave = (grupo.Key.TurmaGuid, grupo.Key.Dia);

            if (chamadasPorChave.TryGetValue(chave, out var chamadaExistente))
            {
                // ── Chamada já existe: atualiza status dos alunos existentes ─────
                var prazoEdicao = chamadaExistente.DataCriacao.AddDays(7);
                if (DateTimeOffset.UtcNow > prazoEdicao)
                {
                    _logger.LogWarning(
                        "[SYNC] Chamada do dia {Data} para turma {TurmaId} ultrapassou o prazo de 7 dias. Ignorando {Count} registros.",
                        grupo.Key.Dia, grupo.Key.TurmaGuid, grupo.Count());

                    foreach (var dto in grupo)
                    {
                        rejeicoes.Add(new SyncRejeicao(
                            dto.Id,
                            $"Chamada do dia {grupo.Key.Dia:dd/MM/yyyy} ultrapassou o prazo de 7 dias para edição."));
                    }

                    continue;
                }

                var registrosPorAluno = chamadaExistente.RegistrosPresenca
                    .ToDictionary(r => r.AlunoId, r => r);

                foreach (var dto in grupo)
                {
                    var alunoGuid = ResolveGuid(dto.AlunoId);
                    if (alunoGuid == Guid.Empty || !alunosDb.TryGetValue(alunoGuid, out var aluno))
                    {
                        _logger.LogWarning("[SYNC] Aluno '{AlunoId}' não encontrado. Registro ignorado.", dto.AlunoId);
                        continue;
                    }

                    if (!registrosPorAluno.TryGetValue(alunoGuid, out var registroExistente))
                    {
                        _logger.LogWarning(
                            "[SYNC] Aluno {AlunoId} não consta na chamada do dia {Data}. Não é permitido adicionar novos alunos.",
                            alunoGuid, grupo.Key.Dia);
                        continue;
                    }

                    var status = ParseStatus(dto.Status);

                    // Sempre mapeia o IdExterno local para o RegistroPresenca existente,
                    // mesmo quando o status já coincide. Isso permite futuros updates.
                    _context.SyncLogs.Add(new SyncLog
                    {
                        Id = Guid.NewGuid(),
                        IdExterno = dto.Id,
                        EntidadeId = registroExistente.Id,
                        TabelaOrigem = "registros_presenca",
                        SincronizadoEm = DateTimeOffset.UtcNow
                    });

                    if (registroExistente.Status == status)
                    {
                        // Status igual: não há alteração, mas o registro foi sincronizado.
                        criados++;
                        continue;
                    }

                    registroExistente.AlterarStatus(status);
                    afetados.Add(alunoGuid);

                    criados++;
                }
            }
            else
            {
                // ── Nova chamada ────────────────────────────────────────────────
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

                    var registro = chamada.RegistrarPresenca(aluno.Id, status);

                    var dataPresenca = ConvertTimestamp(dto.Data).UtcDateTime;
                    aluno.RegistrarPresenca(status, dataPresenca);

                    if (aluno.DomainEvents.Count > 0)
                        alertas++;

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
        }

        return (criados, alertas, afetados);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATED: Registros com status corrigido offline após sync anterior
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<(int Atualizados, HashSet<Guid> Afetados)> ProcessarUpdated(
        List<RegistroPresencaSyncDto> registros,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        var idsExternos = registros.Select(r => r.Id).ToList();
        var mapeamentos = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct);

        var entidadeIds = mapeamentos.Values.ToList();
        var registrosDb = await _context.RegistrosPresenca
            .Include(r => r.Chamada)
            .Where(rp => entidadeIds.Contains(rp.Id))
            .ToDictionaryAsync(rp => rp.Id, ct);

        int atualizados = 0;
        var afetados = new HashSet<Guid>();

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

            // Verifica prazo de 7 dias da Chamada pai
            var prazoEdicao = registroPresenca.Chamada.DataCriacao.AddDays(7);
            if (DateTimeOffset.UtcNow > prazoEdicao)
            {
                _logger.LogWarning(
                    "[SYNC-UPDATE] Chamada {ChamadaId} do dia {Data} ultrapassou o prazo de 7 dias. Ignorando atualização do aluno {AlunoId}.",
                    registroPresenca.Chamada.Id, registroPresenca.Chamada.DataHora.Date, registroPresenca.AlunoId);

                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    $"Chamada do dia {registroPresenca.Chamada.DataHora.Date:dd/MM/yyyy} ultrapassou o prazo de 7 dias para edição."));

                continue;
            }

            try
            {
                if (registroPresenca.Status != novoStatus)
                {
                    registroPresenca.AlterarStatus(novoStatus);
                    afetados.Add(registroPresenca.AlunoId);
                    atualizados++;
                }
            }
            catch (DomainException ex)
            {
                _logger.LogDebug("[SYNC-UPDATE] Skip: {Mensagem}", ex.Message);
            }
        }

        return (atualizados, afetados);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ALUNOS CRIADOS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarAlunosCriados(
        List<AlunoOfflineSyncDto> alunos,
        CancellationToken ct)
    {
        var idsExternos = alunos.Select(a => a.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var novos = alunos.Where(a => !idsJaSincronizados.Contains(a.Id)).ToList();
        if (novos.Count == 0) return 0;

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

    private async Task ValidarOwnershipAsync(
        SyncPushCommand request,
        List<SyncRejeicao> rejeicoes,
        CancellationToken cancellationToken)
    {
        if (_currentUser.Papel == nameof(PapelUsuario.Administrador))
            return;

        if (!Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
            return;

        var idsTurmas = request.Changes.RegistrosPresenca.Created
            .Concat(request.Changes.RegistrosPresenca.Updated)
            .Select(r => r.TurmaId)
            .Where(id => Guid.TryParse(id, out _))
            .Distinct()
            .Select(id => Guid.Parse(id))
            .ToList();

        if (idsTurmas.Count == 0)
            return;

        var turmasPermitidas = await _context.UsuarioTurmas
            .Where(ut => ut.UsuarioId == usuarioId && idsTurmas.Contains(ut.TurmaId))
            .Select(ut => ut.TurmaId)
            .ToHashSetAsync(cancellationToken);

        var turmasNegadas = idsTurmas.Except(turmasPermitidas).ToHashSet();
        if (turmasNegadas.Count == 0)
            return;

        void RejeitarRegistros(IEnumerable<RegistroPresencaSyncDto> registros)
        {
            foreach (var dto in registros)
            {
                if (Guid.TryParse(dto.TurmaId, out var turmaGuid) && turmasNegadas.Contains(turmaGuid))
                {
                    rejeicoes.Add(new SyncRejeicao(
                        dto.Id,
                        "Você não tem permissão para alterar registros desta turma."));
                }
            }
        }

        RejeitarRegistros(request.Changes.RegistrosPresenca.Created);
        RejeitarRegistros(request.Changes.RegistrosPresenca.Updated);
    }

    private async Task RecalcularEstatisticasDosAlunos(
        HashSet<Guid> alunosIds,
        CancellationToken cancellationToken)
    {
        if (alunosIds.Count == 0) return;

        // Carrega registros já persistidos + os adicionados no ChangeTracker
        // para garantir que a recalculagem considere também novos registros
        // criados no mesmo batch de sync antes do SaveChanges final.
        var registrosPersistidos = await _context.RegistrosPresenca
            .Include(r => r.Chamada)
            .Where(r => alunosIds.Contains(r.AlunoId))
            .ToListAsync(cancellationToken);

        var registrosAdicionados = _context.ChangeTracker.Entries<RegistroPresenca>()
            .Where(e => e.State == EntityState.Added && alunosIds.Contains(e.Entity.AlunoId))
            .Select(e => e.Entity)
            .ToList();

        // Garante que registros pendentes tenham a navegação Chamada carregada
        foreach (var registro in registrosAdicionados)
        {
            if (registro.Chamada is null)
            {
                await _context.Entry(registro)
                    .Reference(r => r.Chamada)
                    .LoadAsync(cancellationToken);
            }
        }

        var registros = registrosPersistidos
            .Concat(registrosAdicionados)
            .ToList();

        var registrosPorAluno = registros
            .GroupBy(r => r.AlunoId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var alunos = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var alunoId in alunosIds)
        {
            if (!alunos.TryGetValue(alunoId, out var aluno))
                continue;

            var historico = registrosPorAluno.TryGetValue(alunoId, out var regs)
                ? regs
                : Enumerable.Empty<RegistroPresenca>();

            aluno.RecalcularEstatisticas(historico);
        }
    }

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
