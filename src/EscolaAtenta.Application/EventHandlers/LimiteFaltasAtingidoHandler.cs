using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.EventHandlers;

/// <summary>
/// Handler para o Domain Event LimiteFaltasAtingidoEvent.
/// 
/// Responsabilidade: Criar um AlertaEvasao quando um aluno atinge o limite de faltas.
/// 
/// Decisão de design: Este handler é desacoplado do fluxo de registro de presença.
/// Ele é invocado pelo AppDbContext após o SaveChangesAsync bem-sucedido, garantindo
/// que o alerta só seja criado se o registro de presença foi persistido com sucesso.
/// 
/// Idempotência: Verifica se já existe um alerta não resolvido para o aluno antes
/// de criar um novo, evitando alertas duplicados em caso de retry.
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
        // ── Verificação de idempotência ────────────────────────────────────────
        // Evita criar múltiplos alertas para o mesmo aluno se o evento for
        // disparado mais de uma vez (ex: retry após falha parcial)
        var alertaExistente = await _context.AlertasEvasao
            .AnyAsync(ae =>
                ae.AlunoId == notification.AlunoId &&
                !ae.Resolvido,
                cancellationToken);

        if (alertaExistente)
        {
            _logger.LogInformation(
                "Alerta de evasão já existe para o aluno {AlunoId}. Ignorando evento duplicado.",
                notification.AlunoId);
            return;
        }

        // ── Criação do alerta ──────────────────────────────────────────────────
        var descricao = $"Aluno '{notification.NomeAluno}' atingiu {notification.TotalFaltas} faltas " +
                        $"(limite configurado: {notification.LimiteConfigurado}). " +
                        $"Turma: {notification.TurmaId}. " +
                        $"Data: {notification.OcorridoEm:dd/MM/yyyy HH:mm}.";

        var alerta = new AlertaEvasao(
            id: Guid.NewGuid(),
            alunoId: notification.AlunoId,
            descricao: descricao
        );

        _context.AlertasEvasao.Add(alerta);

        // Salva o alerta — este SaveChanges não dispara novos Domain Events
        // pois AlertaEvasao não possui eventos pendentes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Alerta de evasão criado para o aluno {AlunoId} ({NomeAluno}). " +
            "Total de faltas: {TotalFaltas}/{LimiteConfigurado}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.TotalFaltas,
            notification.LimiteConfigurado);
    }
}
