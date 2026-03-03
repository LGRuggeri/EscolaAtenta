using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.EventHandlers;

/// <summary>
/// Handler para o Domain Event LimiteFaltasAtingidoEvent.
/// 
/// Responsabilidade: Criar ou atualizar um AlertaEvasao quando um aluno atinge
/// o limite de faltas. Nunca duplica alertas pendentes — se já existe um alerta
/// não resolvido, atualiza o nível (escalada) ao invés de criar um novo.
/// 
/// Fluxo de idempotência:
/// 1. Se NÃO existe nenhum alerta de Evasão pendente → cria um novo.
/// 2. Se JÁ existe um alerta de Evasão pendente → escala o nível existente.
///    Isso garante que o dashboard nunca mostre dois alertas abertos para
///    o mesmo aluno pelo mesmo motivo (evasão), mesmo que a severidade evolua
///    de Aviso para Intermediário, Vermelho ou Preto.
/// 
/// Decisão de design: Este handler é desacoplado do fluxo de registro de presença.
/// Ele é invocado pelo AppDbContext após o SaveChangesAsync bem-sucedido, garantindo
/// que o alerta só seja criado/atualizado se o registro de presença foi persistido
/// com sucesso.
/// </summary>
public class LimiteFaltasAtingidoHandler : INotificationHandler<LimiteFaltasAtingidoEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<LimiteFaltasAtingidoHandler> _logger;

    public LimiteFaltasAtingidoHandler(
        AppDbContext context,
        ILogger<LimiteFaltasAtingidoHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        LimiteFaltasAtingidoEvent notification,
        CancellationToken cancellationToken)
    {
        // ── Guarda de Idempotência com Escalada ───────────────────────────────
        // Buscamos qualquer alerta de Evasão não resolvido para este aluno,
        // independentemente do nível atual. O nível é intencionalmente ignorado:
        // se o aluno agravar a situação (ex: Aviso → Vermelho), queremos apenas
        // atualizar o alerta existente, não criar um segundo.
        var alertaPendente = await _context.AlertasEvasao
            .FirstOrDefaultAsync(a => a.AlunoId == notification.AlunoId
                                   && !a.Resolvido
                                   && a.Tipo == TipoAlerta.Evasao, cancellationToken);

        if (alertaPendente is not null)
        {
            // ── Escalada de Nível ──────────────────────────────────────────────
            // O alerta já existe: atualizamos o nível ao invés de criar outro.
            // O método AtualizarNivel() garante a invariante de domínio
            // (não permite escalar alerta já resolvido).
            alertaPendente.AtualizarNivel(notification.Nivel, notification.MotivoExato);

            _logger.LogWarning(
                "Alerta de evasão existente atualizado para o aluno {AlunoId} ({NomeAluno}). " +
                "Nível escalado para: {Nivel}. Total de faltas consecutivas: {TotalFaltas}.",
                notification.AlunoId,
                notification.NomeAluno,
                notification.Nivel,
                notification.TotalFaltas);

            return;
        }

        // ── Criação do Novo Alerta ─────────────────────────────────────────────
        var alerta = AlertaEvasao.CriarAlertaAluno(
            alunoId: notification.AlunoId,
            turmaId: notification.TurmaId,
            nivel: notification.Nivel,
            motivo: notification.MotivoExato
        );

        _context.AlertasEvasao.Add(alerta);

        // Observação: Não chamamos SaveChanges() aqui! O alerta foi apenas 
        // adicionado ao AppDbContext. Ele será persistido na MESMA transação do 
        // evento pai via atomicity do `SaveChangesAsync` na camada do Entity Framework.

        _logger.LogWarning(
            "Novo alerta de evasão enfileirado no DbContext para o aluno {AlunoId} ({NomeAluno}). " +
            "Nível: {Nivel}. Total de faltas consecutivas: {TotalFaltas}/{LimiteConfigurado}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.Nivel,
            notification.TotalFaltas,
            notification.LimiteConfigurado);
    }
}
