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

    public SyncPushHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<SyncPushHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<SyncPushResult> Handle(SyncPushCommand request, CancellationToken cancellationToken)
    {
        var created = request.Changes.RegistrosPresenca.Created;
        var updated = request.Changes.RegistrosPresenca.Updated;

        if (created.Count == 0 && updated.Count == 0)
            return new SyncPushResult(0, 0);

        // ── Segurança: Responsável extraído do JWT, nunca do cliente ─────────
        var responsavelId = _currentUser.EstaAutenticado
            && Guid.TryParse(_currentUser.UsuarioId, out var parsedUserId)
            ? parsedUserId
            : throw new DomainException("Usuário não autenticado. Sync requer autenticação.");

        int totalSincronizados = 0;
        int alertasGerados = 0;

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

        _logger.LogInformation(
            "[SYNC-PUSH] Concluído — Created={Created} Updated={Updated} Alertas={Alertas} Responsavel={User}",
            created.Count, updated.Count, alertasGerados, responsavelId);

        return new SyncPushResult(totalSincronizados, alertasGerados);
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

        // 2. Agrupar por (TurmaId + Dia) → uma Chamada por turma por dia
        //    Necessário porque chamada.RegistrarPresenca() impede aluno duplicado.
        //    Se o monitor fez chamada na segunda e na terça offline, são Chamadas distintas.
        var grupos = registrosNovos
            .GroupBy(r => new
            {
                r.TurmaId,
                Dia = ConvertTimestamp(r.Data).Date
            })
            .ToList();

        // 3. Validar turmas existentes
        var turmaIds = grupos.Select(g => g.Key.TurmaId).Distinct().ToList();
        var turmasExistentes = await _context.Turmas
            .Where(t => turmaIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        // 4. Carregar alunos necessários (para atualizar contadores)
        var todosAlunoIds = registrosNovos.Select(r => r.AlunoId).Distinct().ToList();
        var alunosDb = await _context.Alunos
            .Where(a => todosAlunoIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        int criados = 0;
        int alertas = 0;

        foreach (var grupo in grupos)
        {
            if (!turmasExistentes.Contains(grupo.Key.TurmaId))
            {
                _logger.LogWarning("[SYNC] Turma {TurmaId} não encontrada. Ignorando {Count} registros.",
                    grupo.Key.TurmaId, grupo.Count());
                continue;
            }

            // Converte o timestamp do primeiro registro do grupo para a DataHora da Chamada
            var primeiroTimestamp = grupo.Min(r => r.Data);
            var dataHoraChamada = ConvertTimestamp(primeiroTimestamp);

            // Cria a Chamada via domínio (preserva invariantes de duplicidade)
            var chamada = new Chamada(
                id: Guid.NewGuid(),
                dataHora: dataHoraChamada,
                turmaId: grupo.Key.TurmaId,
                responsavelId: responsavelId
            );

            _context.Chamadas.Add(chamada);

            foreach (var dto in grupo)
            {
                if (!alunosDb.TryGetValue(dto.AlunoId, out var aluno))
                {
                    _logger.LogWarning("[SYNC] Aluno {AlunoId} não encontrado. Registro ignorado.", dto.AlunoId);
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
