using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
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
        var turmasAtualizadas = request.Changes.Turmas.Updated;
        var alunosCriados = request.Changes.Alunos.Created;
        var alunosAtualizados = request.Changes.Alunos.Updated;
        var created = request.Changes.RegistrosPresenca.Created;
        var updated = request.Changes.RegistrosPresenca.Updated;

        if (turmasCriadas.Count == 0 && turmasAtualizadas.Count == 0 &&
            alunosCriados.Count == 0 && alunosAtualizados.Count == 0 &&
            created.Count == 0 && updated.Count == 0)
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
        var idsRejeitadosPorOwnership = await ValidarOwnershipAsync(request, rejeicoes, cancellationToken);

        await _lockProvider.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            // ── TURMAS CRIADAS OFFLINE ────────────────────────────────────────────
            if (turmasCriadas.Count > 0)
            {
                totalSincronizados += await ProcessarTurmasCriadas(turmasCriadas, responsavelId, rejeicoes, cancellationToken);
            }

            // ── TURMAS ATUALIZADAS OFFLINE ────────────────────────────────────────
            if (request.Changes.Turmas.Updated.Count > 0)
            {
                totalSincronizados += await ProcessarTurmasAtualizadas(request.Changes.Turmas.Updated, rejeicoes, cancellationToken);
            }

            // ── ALUNOS CRIADOS OFFLINE ────────────────────────────────────────────
            if (alunosCriados.Count > 0)
            {
                totalSincronizados += await ProcessarAlunosCriados(alunosCriados, rejeicoes, cancellationToken);
            }

            // ── ALUNOS ATUALIZADOS OFFLINE ────────────────────────────────────────
            if (request.Changes.Alunos.Updated.Count > 0)
            {
                totalSincronizados += await ProcessarAlunosAtualizados(request.Changes.Alunos.Updated, rejeicoes, cancellationToken);
            }

            // ── CREATED ──────────────────────────────────────────────────────────
            if (created.Count > 0)
            {
                var (criados, afetados) = await ProcessarCreated(created, responsavelId, idsRejeitadosPorOwnership, rejeicoes, cancellationToken);
                totalSincronizados += criados;
                foreach (var id in afetados) alunosAfetados.Add(id);
            }

            // ── UPDATED ──────────────────────────────────────────────────────────
            if (updated.Count > 0)
            {
                var (atualizados, afetados) = await ProcessarUpdated(updated, idsRejeitadosPorOwnership, rejeicoes, cancellationToken);
                totalSincronizados += atualizados;
                foreach (var id in afetados) alunosAfetados.Add(id);
            }

            // ── Recálculo de estatísticas dos alunos afetados ────────────────────
            if (alunosAfetados.Count > 0)
            {
                await RecalcularEstatisticasDosAlunos(alunosAfetados, cancellationToken);

                // Conta alertas gerados pela recalculagem (apenas LimiteFaltasAtingidoEvent)
                var alunosDb = await _context.Alunos
                    .Where(a => alunosAfetados.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                foreach (var alunoId in alunosAfetados)
                {
                    if (alunosDb.TryGetValue(alunoId, out var aluno)
                        && aluno.DomainEvents.OfType<LimiteFaltasAtingidoEvent>().Any())
                    {
                        alertasGerados++;
                    }
                }
            }

            // Rejeições de negócio não abortam o batch: os registros válidos são
            // persistidos e as rejeições são retornadas para que o app as trate.
            // O rollback só ocorre em falhas técnicas (exceções do EF/database).
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
        Guid responsavelId,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        // Apenas Administrador pode criar turmas via sync. Monitores podem usar
        // turmas existentes, mas não criar novas unidades educacionais no sistema.
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
        {
            foreach (var dto in turmas)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Apenas administradores podem criar turmas offline."));
            }
            return 0;
        }

        var idsExternos = turmas.Select(t => t.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var novas = turmas
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .Where(t => !idsJaSincronizados.Contains(t.Id))
            .ToList();
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

            // P1: vincula a turma ao usuário que a criou, permitindo que ele
            // realize chamadas nela imediatamente (offline-first).
            _context.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), responsavelId, turma.Id));

            criados++;
        }

        _logger.LogInformation("[SYNC-TURMA] {Count} turma(s) criada(s) offline sincronizadas.", criados);
        return criados;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TURMAS ATUALIZADAS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarTurmasAtualizadas(
        List<TurmaOfflineSyncDto> turmas,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
        {
            foreach (var dto in turmas)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Apenas administradores podem editar turmas offline."));
            }
            return 0;
        }

        var idsExternos = turmas.Select(t => t.Id).Distinct().ToList();

        // Resolve IDs externos: SyncLog para turmas criadas offline; GUID direto
        // para turmas que vieram do servidor (o WatermelonDB usa o próprio server_id).
        var mapeamentos = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno) && s.TabelaOrigem == "turmas")
            .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct);

        Guid ResolverTurmaId(string idExterno)
        {
            if (mapeamentos.TryGetValue(idExterno, out var entidadeId))
                return entidadeId;

            if (Guid.TryParse(idExterno, out var guid))
                return guid;

            return Guid.Empty;
        }

        int atualizadas = 0;

        foreach (var dto in turmas.GroupBy(t => t.Id).Select(g => g.First()))
        {
            var entidadeId = ResolverTurmaId(dto.Id);
            if (entidadeId == Guid.Empty)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Turma não encontrada para atualização offline."));
                continue;
            }

            var turma = await _context.Turmas.FindAsync([entidadeId], ct);
            if (turma is null)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Turma não encontrada para atualização offline."));
                continue;
            }

            var turno = string.IsNullOrWhiteSpace(dto.Turno) ? turma.Turno : dto.Turno;
            var anoLetivo = dto.AnoLetivo > 0 ? dto.AnoLetivo : turma.AnoLetivo;

            turma.Atualizar(dto.Nome, turno, anoLetivo);
            atualizadas++;
        }

        _logger.LogInformation("[SYNC-TURMA] {Count} turma(s) atualizada(s) offline.", atualizadas);
        return atualizadas;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATED: Novos registros de presença gerados offline
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<(int Criados, HashSet<Guid> Afetados)> ProcessarCreated(
        List<RegistroPresencaSyncDto> registros,
        Guid responsavelId,
        HashSet<string> idsRejeitados,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        var idsExternos = registros.Select(r => r.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var registrosNovos = registros
            .Where(r => !idsRejeitados.Contains(r.Id) && !idsJaSincronizados.Contains(r.Id))
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();

        if (registrosNovos.Count == 0)
            return (0, []);

        var idsExternosTurma = registrosNovos
            .Select(r => r.TurmaId)
            .Where(id => !Guid.TryParse(id, out _))
            .Distinct()
            .ToList();

        var idsExternosAluno = registrosNovos
            .Select(r => r.AlunoId)
            .Where(id => !Guid.TryParse(id, out _))
            .Distinct()
            .ToList();

        var syncLogTurmas = ResolverMapeamentoSyncLog(idsExternosTurma, "turmas");
        var syncLogAlunos = ResolverMapeamentoSyncLog(idsExternosAluno, "alunos");

        Guid ResolveGuid(string id, string idExternoRegistro, string campo)
        {
            if (Guid.TryParse(id, out var guid)) return guid;

            var mapa = campo == "Turma" ? syncLogTurmas : syncLogAlunos;
            if (mapa.TryGetValue(id, out var resolved)) return resolved;

            rejeicoes.Add(new SyncRejeicao(
                idExternoRegistro,
                $"{campo} '{id}' não encontrado no servidor."));
            return Guid.Empty;
        }

        var grupos = registrosNovos
            .GroupBy(r => new
            {
                TurmaGuid = ResolveGuid(r.TurmaId, r.Id, "Turma"),
                // DataChamada é DateTime (parte da data UTC). Normaliza para o mesmo tipo
                // para garantir comparação correta com chamadas existentes.
                Dia = ConvertTimestamp(r.Data).UtcDateTime.Date
            })
            .ToList();

        var turmaGuids = grupos.Select(g => g.Key.TurmaGuid).Where(g => g != Guid.Empty).Distinct().ToList();
        var turmasExistentes = CarregarTurmasComChangeTracker(turmaGuids)
            .Select(t => t.Id)
            .ToHashSet();

        var todosAlunoGuids = registrosNovos
            .Select(r => ResolveGuid(r.AlunoId, r.Id, "Aluno"))
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        var alunosDb = CarregarAlunosComChangeTracker(todosAlunoGuids);

        // Carrega chamadas existentes para os grupos (turma + dia)
        // Filtragem por data é feita em memória para compatibilidade com SQLite/DateTimeOffset.
        var chamadasExistentes = CarregarChamadasComChangeTracker(turmaGuids);

        var datasDosGrupos = grupos.Select(g => g.Key.Dia).ToHashSet();
        var chamadasPorChave = chamadasExistentes
            .Where(c => datasDosGrupos.Contains(c.DataChamada))
            .GroupBy(c => (c.TurmaId, c.DataChamada))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.DataCriacao).ThenBy(c => c.Id).First());

        int criados = 0;
        var afetados = new HashSet<Guid>();

        foreach (var grupo in grupos)
        {
            if (grupo.Key.TurmaGuid == Guid.Empty || !turmasExistentes.Contains(grupo.Key.TurmaGuid))
            {
                _logger.LogWarning("[SYNC] Turma '{TurmaId}' não encontrada. Ignorando {Count} registros.",
                    grupo.First().TurmaId, grupo.Count());
                continue;
            }

            // P2: a chamada representa o dia letivo, não o horário exato do registro.
            var dataHoraChamada = new DateTimeOffset(grupo.Key.Dia, TimeSpan.Zero);

            // P2: rejeita datas futuras — isso criaria presenças antecipadas e
            // poderia mover o ciclo trimestral para frente, corrompendo contadores.
            if (dataHoraChamada.Date > DateTimeOffset.UtcNow.Date)
            {
                _logger.LogWarning(
                    "[SYNC] Data futura rejeitada para turma {TurmaId}: {Data}. Ignorando {Count} registros.",
                    grupo.Key.TurmaGuid, grupo.Key.Dia, grupo.Count());

                foreach (var dto in grupo)
                {
                    rejeicoes.Add(new SyncRejeicao(
                        dto.Id,
                        $"A data da chamada não pode ser posterior ao dia atual."));
                }

                continue;
            }

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

                // P1: se a chamada existente está vazia (por exemplo, criada por um bug
                // anterior ou por sync interrompido), permitimos preenchê-la com os
                // registros enviados pelo app, desde que os alunos pertençam à turma.
                var chamadaEstaVazia = registrosPorAluno.Count == 0;

                foreach (var dto in grupo)
                {
                    var alunoGuid = ResolveGuid(dto.AlunoId, dto.Id, "Aluno");
                    if (alunoGuid == Guid.Empty || !alunosDb.TryGetValue(alunoGuid, out var aluno))
                    {
                        continue;
                    }

                    var status = ParseStatus(dto.Status);

                    if (chamadaEstaVazia)
                    {
                        if (aluno.TurmaId != grupo.Key.TurmaGuid)
                        {
                            rejeicoes.Add(new SyncRejeicao(
                                dto.Id,
                                "Aluno não pertence à turma da chamada."));
                            continue;
                        }

                        var registro = chamadaExistente.RegistrarPresenca(aluno.Id, status);
                        afetados.Add(alunoGuid);

                        _context.SyncLogs.Add(new SyncLog
                        {
                            Id = Guid.NewGuid(),
                            IdExterno = dto.Id,
                            EntidadeId = registro.Id,
                            TabelaOrigem = "registros_presenca",
                            SincronizadoEm = DateTimeOffset.UtcNow
                        });

                        criados++;
                        continue;
                    }

                    if (!registrosPorAluno.TryGetValue(alunoGuid, out var registroExistente))
                    {
                        rejeicoes.Add(new SyncRejeicao(
                            dto.Id,
                            $"Aluno não consta na chamada do dia {grupo.Key.Dia:dd/MM/yyyy}. Não é permitido adicionar novos alunos."));
                        continue;
                    }

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
                    var alunoGuid = ResolveGuid(dto.AlunoId, dto.Id, "Aluno");
                    if (alunoGuid == Guid.Empty || !alunosDb.TryGetValue(alunoGuid, out var aluno))
                    {
                        continue;
                    }

                    if (aluno.TurmaId != grupo.Key.TurmaGuid)
                    {
                        rejeicoes.Add(new SyncRejeicao(
                            dto.Id,
                            "Aluno não pertence à turma da chamada."));
                        continue;
                    }

                    var status = ParseStatus(dto.Status);

                    var registro = chamada.RegistrarPresenca(aluno.Id, status);

                    aluno.RegistrarPresenca(status, dataHoraChamada.UtcDateTime);
                    afetados.Add(alunoGuid);

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

                // P1: se nenhum registro da nova chamada foi aceito, descarta a chamada
                // para nao persistir uma chamada vazia que poluiria relatorios e dashboards.
                if (chamada.RegistrosPresenca.Count == 0)
                {
                    _context.Chamadas.Remove(chamada);
                }
            }
        }

        return (criados, afetados);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATED: Registros com status corrigido offline após sync anterior
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<(int Atualizados, HashSet<Guid> Afetados)> ProcessarUpdated(
        List<RegistroPresencaSyncDto> registros,
        HashSet<string> idsRejeitados,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        var registrosPermitidos = registros.Where(r => !idsRejeitados.Contains(r.Id)).ToList();

        var idsExternos = registrosPermitidos.Select(r => r.Id).ToList();
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

        foreach (var dto in registrosPermitidos)
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

            // P1: autoriza pela turma real do registro mapeado, não pelo TurmaId
            // enviado pelo cliente. Isso impede que um usuário malicioso atualize
            // um registro de outra turma usando um TurmaId permitido.
            if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
                && Guid.TryParse(_currentUser.UsuarioId, out var usuarioId)
                && !await _context.UsuarioTurmas.AnyAsync(
                    ut => ut.TurmaId == registroPresenca.Chamada.TurmaId && ut.UsuarioId == usuarioId, ct))
            {
                _logger.LogWarning(
                    "[SYNC-UPDATE] Usuário {UsuarioId} não tem permissão para atualizar registro da turma {TurmaId}.",
                    usuarioId, registroPresenca.Chamada.TurmaId);

                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Você não tem permissão para alterar registros desta turma."));

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
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        // Apenas Administrador pode criar alunos via sync.
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
        {
            foreach (var dto in alunos)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Apenas administradores podem criar alunos offline."));
            }
            return 0;
        }

        var idsExternos = alunos.Select(a => a.Id).ToList();
        var idsJaSincronizados = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno))
            .Select(s => s.IdExterno)
            .ToHashSetAsync(ct);

        var novos = alunos
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .Where(a => !idsJaSincronizados.Contains(a.Id))
            .ToList();
        if (novos.Count == 0) return 0;

        var turmaIdsLocais = novos
            .Where(a => !Guid.TryParse(a.TurmaId, out _))
            .Select(a => a.TurmaId)
            .Distinct()
            .ToList();

        var syncLogsTurma = ResolverMapeamentoSyncLog(turmaIdsLocais, "turmas");

        var turmaGuidsCandidatos = new HashSet<Guid>();
        foreach (var dto in novos)
        {
            if (Guid.TryParse(dto.TurmaId, out var g))
                turmaGuidsCandidatos.Add(g);
            else if (syncLogsTurma.TryGetValue(dto.TurmaId, out var mapped))
                turmaGuidsCandidatos.Add(mapped);
        }

        var turmasExistentes = CarregarTurmasComChangeTracker(turmaGuidsCandidatos.ToList())
            .ToDictionary(t => t.Id, t => t.AnoLetivo);

        int criados = 0;

        foreach (var dto in novos)
        {
            Guid turmaGuid;
            if (!Guid.TryParse(dto.TurmaId, out turmaGuid))
            {
                if (!syncLogsTurma.TryGetValue(dto.TurmaId, out turmaGuid))
                {
                    rejeicoes.Add(new SyncRejeicao(
                        dto.Id,
                        $"Turma '{dto.TurmaId}' não encontrada para criação do aluno."));
                    continue;
                }
            }

            if (!turmasExistentes.TryGetValue(turmaGuid, out var anoLetivo))
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    $"Turma '{dto.TurmaId}' não existe no servidor."));
                continue;
            }

            var aluno = new Aluno(Guid.NewGuid(), dto.Nome, null, turmaGuid);
            _context.Alunos.Add(aluno);

            // Cria o histórico de matrícula para que o aluno apareça em relatórios
            // por turma e no histórico de turmas imediatamente após o sync.
            aluno.Matricular(
                turmaGuid,
                anoLetivo,
                new DateTime(anoLetivo, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "Matrícula via sincronização offline");

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
    // ALUNOS ATUALIZADOS OFFLINE
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<int> ProcessarAlunosAtualizados(
        List<AlunoOfflineSyncDto> alunos,
        List<SyncRejeicao> rejeicoes,
        CancellationToken ct)
    {
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
        {
            foreach (var dto in alunos)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Apenas administradores podem editar alunos offline."));
            }
            return 0;
        }

        var idsExternos = alunos.Select(a => a.Id).Distinct().ToList();

        // Resolve IDs externos: SyncLog para alunos criados offline; GUID direto
        // para alunos que vieram do servidor (o WatermelonDB usa o próprio server_id).
        var mapeamentos = await _context.SyncLogs
            .Where(s => idsExternos.Contains(s.IdExterno) && s.TabelaOrigem == "alunos")
            .ToDictionaryAsync(s => s.IdExterno, s => s.EntidadeId, ct);

        Guid ResolverAlunoId(string idExterno)
        {
            if (mapeamentos.TryGetValue(idExterno, out var entidadeId))
                return entidadeId;

            if (Guid.TryParse(idExterno, out var guid))
                return guid;

            return Guid.Empty;
        }

        int atualizados = 0;

        foreach (var dto in alunos.GroupBy(a => a.Id).Select(g => g.First()))
        {
            var entidadeId = ResolverAlunoId(dto.Id);
            if (entidadeId == Guid.Empty)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Aluno não encontrado para atualização offline."));
                continue;
            }

            var aluno = await _context.Alunos.FindAsync([entidadeId], ct);
            if (aluno is null)
            {
                rejeicoes.Add(new SyncRejeicao(
                    dto.Id,
                    "Aluno não encontrado para atualização offline."));
                continue;
            }

            aluno.Atualizar(dto.Nome, aluno.Matricula);
            atualizados++;
        }

        _logger.LogInformation("[SYNC-ALUNO] {Count} aluno(s) atualizado(s) offline.", atualizados);
        return atualizados;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolve IDs externos (WatermelonDB) para GUIDs do servidor, considerando
    /// tanto os SyncLogs já persistidos quanto os adicionados no ChangeTracker
    /// durante o mesmo batch. Isso permite criar turmas, alunos e presenças
    /// offline no mesmo push sem depender de round-trips ao banco.
    /// </summary>
    private Dictionary<string, Guid> ResolverMapeamentoSyncLog(
        IEnumerable<string> idsExternos,
        string tabelaOrigem)
    {
        var ids = idsExternos
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, Guid>();

        // SyncLogs já persistidos no banco.
        var doBanco = _context.SyncLogs
            .Where(s => ids.Contains(s.IdExterno) && s.TabelaOrigem == tabelaOrigem)
            .ToDictionary(s => s.IdExterno, s => s.EntidadeId);

        // SyncLogs adicionados no mesmo batch (ainda não persistidos).
        var doChangeTracker = _context.ChangeTracker.Entries<SyncLog>()
            .Where(e => e.State == EntityState.Added
                     && e.Entity.TabelaOrigem == tabelaOrigem
                     && ids.Contains(e.Entity.IdExterno))
            .Select(e => e.Entity)
            .ToDictionary(s => s.IdExterno, s => s.EntidadeId);

        // Merge: entidades do batch atual prevalecem sobre o banco.
        foreach (var item in doChangeTracker)
        {
            doBanco[item.Key] = item.Value;
        }

        return doBanco;
    }

    /// <summary>
    /// Carrega turmas do banco e do ChangeTracker para permitir que registros
    /// de presença offline sejam vinculados a turmas criadas no mesmo batch.
    /// </summary>
    private List<Turma> CarregarTurmasComChangeTracker(List<Guid> ids)
    {
        if (ids.Count == 0)
            return new List<Turma>();

        var doBanco = _context.Turmas
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToList();

        var doChangeTracker = _context.ChangeTracker.Entries<Turma>()
            .Where(e => e.State == EntityState.Added && ids.Contains(e.Entity.Id))
            .Select(e => e.Entity)
            .ToList();

        return doBanco
            .Concat(doChangeTracker)
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Carrega alunos do banco e do ChangeTracker para permitir que registros
    /// de presença offline sejam vinculados a alunos criados no mesmo batch.
    /// </summary>
    private Dictionary<Guid, Aluno> CarregarAlunosComChangeTracker(List<Guid> ids)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, Aluno>();

        var doBanco = _context.Alunos
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionary(a => a.Id);

        var doChangeTracker = _context.ChangeTracker.Entries<Aluno>()
            .Where(e => e.State == EntityState.Added && ids.Contains(e.Entity.Id))
            .Select(e => e.Entity)
            .ToDictionary(a => a.Id);

        foreach (var item in doChangeTracker)
        {
            doBanco[item.Key] = item.Value;
        }

        return doBanco;
    }

    /// <summary>
    /// Carrega chamadas do banco e do ChangeTracker para permitir que novos
    /// registros offline sejam agrupados com chamadas criadas no mesmo batch.
    /// </summary>
    private List<Chamada> CarregarChamadasComChangeTracker(List<Guid> turmaIds)
    {
        if (turmaIds.Count == 0)
            return new List<Chamada>();

        var doBanco = _context.Chamadas
            .Include(c => c.RegistrosPresenca)
            .Where(c => turmaIds.Contains(c.TurmaId))
            .ToList();

        var doChangeTracker = _context.ChangeTracker.Entries<Chamada>()
            .Where(e => e.State == EntityState.Added && turmaIds.Contains(e.Entity.TurmaId))
            .Select(e => e.Entity)
            .ToList();

        // Eager-load registros de chamadas do ChangeTracker
        foreach (var chamada in doChangeTracker)
        {
            if (!_context.Entry(chamada).Collection(c => c.RegistrosPresenca).IsLoaded)
            {
                _context.Entry(chamada).Collection(c => c.RegistrosPresenca).Load();
            }
        }

        return doBanco
            .Concat(doChangeTracker)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<HashSet<string>> ValidarOwnershipAsync(
        SyncPushCommand request,
        List<SyncRejeicao> rejeicoes,
        CancellationToken cancellationToken)
    {
        var idsRejeitados = new HashSet<string>();
        if (_currentUser.Papel == nameof(PapelUsuario.Administrador))
            return idsRejeitados;

        if (!Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
            return idsRejeitados;

        // Coleta todos os IDs de turma enviados (GUID ou externo do WatermelonDB)
        var idsTurmasBrutos = request.Changes.RegistrosPresenca.Created
            .Concat(request.Changes.RegistrosPresenca.Updated)
            .Select(r => r.TurmaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (idsTurmasBrutos.Count == 0)
            return idsRejeitados;

        // Separa GUIDs válidos de IDs externos
        var idsGuid = new List<Guid>();
        var idsExternos = new List<string>();
        foreach (var id in idsTurmasBrutos)
        {
            if (Guid.TryParse(id, out var guid))
                idsGuid.Add(guid);
            else
                idsExternos.Add(id);
        }

        // Resolve IDs externos via SyncLog (tabela turmas), incluindo turmas criadas
        // no mesmo batch que ainda não foram persistidas no banco.
        var mapaExternos = ResolverMapeamentoSyncLog(idsExternos, "turmas");

        // IDs externos que serão criados neste mesmo batch são permitidos:
        // a turma ainda não existe, mas será criada em ProcessarTurmasCriadas
        // e vinculada ao usuário que a criou.
        var idsNovasTurmasNoBatch = request.Changes.Turmas.Created
            .Select(t => t.Id)
            .ToHashSet();

        var externosSemMapeamento = idsExternos
            .Where(id => !mapaExternos.ContainsKey(id) && !idsNovasTurmasNoBatch.Contains(id))
            .ToHashSet();

        // Consolida todos os GUIDs de turma para verificação
        var guidsTurmas = new List<Guid>(idsGuid);
        guidsTurmas.AddRange(mapaExternos.Values);
        guidsTurmas = guidsTurmas.Distinct().ToList();

        if (guidsTurmas.Count == 0 && externosSemMapeamento.Count == 0)
            return idsRejeitados;

        var turmasPermitidas = guidsTurmas.Count > 0
            ? await _context.UsuarioTurmas
                .Where(ut => ut.UsuarioId == usuarioId && guidsTurmas.Contains(ut.TurmaId))
                .Select(ut => ut.TurmaId)
                .ToHashSetAsync(cancellationToken)
            : new HashSet<Guid>();

        var turmasNegadas = guidsTurmas.Except(turmasPermitidas).ToHashSet();

        if (turmasNegadas.Count == 0 && externosSemMapeamento.Count == 0)
            return idsRejeitados;

        void RejeitarRegistros(IEnumerable<RegistroPresencaSyncDto> registros)
        {
            foreach (var dto in registros)
            {
                var turmaId = dto.TurmaId;
                if (string.IsNullOrWhiteSpace(turmaId))
                    continue;

                bool rejeitar = false;
                if (Guid.TryParse(turmaId, out var guid))
                {
                    if (turmasNegadas.Contains(guid))
                        rejeitar = true;
                }
                else if (externosSemMapeamento.Contains(turmaId))
                {
                    rejeitar = true;
                }
                else if (mapaExternos.TryGetValue(turmaId, out var guidResolvido)
                      && turmasNegadas.Contains(guidResolvido))
                {
                    rejeitar = true;
                }

                if (rejeitar)
                {
                    rejeicoes.Add(new SyncRejeicao(
                        dto.Id,
                        "Você não tem permissão para alterar registros desta turma."));
                    idsRejeitados.Add(dto.Id);
                }
            }
        }

        RejeitarRegistros(request.Changes.RegistrosPresenca.Created);
        RejeitarRegistros(request.Changes.RegistrosPresenca.Updated);

        return idsRejeitados;
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

        var alunosDb = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Alunos criados no mesmo batch ainda nao estao persistidos no banco;
        // mescla entidades Added do ChangeTracker para garantir recalculo correto.
        var alunosAdicionados = _context.ChangeTracker.Entries<Aluno>()
            .Where(e => e.State == EntityState.Added && alunosIds.Contains(e.Entity.Id))
            .Select(e => e.Entity)
            .ToDictionary(a => a.Id);

        foreach (var item in alunosAdicionados)
        {
            alunosDb[item.Key] = item.Value;
        }

        foreach (var alunoId in alunosIds)
        {
            if (!alunosDb.TryGetValue(alunoId, out var aluno))
                continue;

            var historico = registrosPorAluno.TryGetValue(alunoId, out var regs)
                ? regs
                : Enumerable.Empty<RegistroPresenca>();

            aluno.RecalcularEstatisticas(historico);

            // Reconcilia alertas pendentes com os contadores finais, rebaixando
            // o nível quando o contador cai para um threshold inferior, resolvendo
            // quando cai abaixo de todos os thresholds e criando/escalando quando
            // atinge um threshold.
            aluno.ReconciliarAlertasPendentes();
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
