using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.EventHandlers;

/// <summary>
/// Handler para o Domain Event LimiteAtrasosAtingidoEvent.
/// 
/// Responsabilidade: Criar ou atualizar um AlertaEvasao (Tipo = Atraso) quando
/// um aluno atinge o limiar de atrasos no trimestre. Nunca duplica alertas
/// pendentes — se já existe um alerta de Atraso não resolvido, atualiza o nível
/// ao invés de criar um novo.
/// 
/// Thresholds disparados pelo Domínio (Aluno.VerificarLimiteAtrasos):
/// - 3 atrasos no trimestre → Nível Aviso
/// - 6 atrasos no trimestre → Nível Intermediário
/// 
/// Fluxo de idempotência (espelha o LimiteFaltasAtingidoHandler):
/// 1. Se NÃO existe nenhum alerta de Atraso pendente → cria um novo.
/// 2. Se JÁ existe um alerta de Atraso pendente → escala o nível existente.
/// </summary>
public class LimiteAtrasosAtingidoHandler : INotificationHandler<LimiteAtrasosAtingidoEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<LimiteAtrasosAtingidoHandler> _logger;

    public LimiteAtrasosAtingidoHandler(
        AppDbContext context,
        ILogger<LimiteAtrasosAtingidoHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        LimiteAtrasosAtingidoEvent notification,
        CancellationToken cancellationToken)
    {
        // ── Guarda de Idempotência com Escalada ───────────────────────────────
        // Buscamos qualquer alerta de Atraso não resolvido para este aluno.
        // Se existe: escalamos o nível. Se não existe: criamos um novo.
        var alertaPendente = await _context.AlertasEvasao
            .FirstOrDefaultAsync(a => a.AlunoId == notification.AlunoId
                                   && !a.Resolvido
                                   && a.Tipo == TipoAlerta.Atraso, cancellationToken);

        if (alertaPendente is not null)
        {
            // ── Escalada de Nível ──────────────────────────────────────────────
            alertaPendente.AtualizarNivel(notification.Nivel, notification.MotivoExato);

            _logger.LogWarning(
                "Alerta de atraso existente atualizado para o aluno {AlunoId} ({NomeAluno}). " +
                "Nível escalado para: {Nivel}. Total de atrasos no trimestre: {TotalAtrasos}.",
                notification.AlunoId,
                notification.NomeAluno,
                notification.Nivel,
                notification.TotalAtrasos);

            return;
        }

        // ── Criação do Novo Alerta ─────────────────────────────────────────────
        var alerta = AlertaEvasao.CriarAlertaAtraso(
            alunoId: notification.AlunoId,
            turmaId: notification.TurmaId,
            nivel: notification.Nivel,
            motivo: notification.MotivoExato
        );

        _context.AlertasEvasao.Add(alerta);

        _logger.LogWarning(
            "Novo alerta de atraso enfileirado no DbContext para o aluno {AlunoId} ({NomeAluno}). " +
            "Nível: {Nivel}. Total de atrasos no trimestre: {TotalAtrasos}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.Nivel,
            notification.TotalAtrasos);
    }
}
