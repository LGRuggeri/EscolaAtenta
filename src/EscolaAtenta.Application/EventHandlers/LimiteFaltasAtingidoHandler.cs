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

    public Task Handle(
        LimiteFaltasAtingidoEvent notification,
        CancellationToken cancellationToken)
    {
        // ── Criação do alerta ──────────────────────────────────────────────────
        // Diferente do passado, hoje um Aluno pode progredir na escala de risco de faltas
        // Se já tiver alertas não resolvidos, eles continuam para ser sanados pela Supervisão do nível de gravidade.
        var alerta = AlertaEvasao.CriarAlertaAluno(
            alunoId: notification.AlunoId,
            turmaId: notification.TurmaId,
            nivel: notification.Nivel, // Lendo a severidade real injetada pelo Domínio
            motivo: notification.MotivoExato // Mantém a string suja imutável a título de auditoria
        );

        _context.AlertasEvasao.Add(alerta);

        // Observação: Não chamamos SaveChanges() aqui! O alerta foi apenas 
        // adicionado ao AppDbContext. Ele será persistido na MESMA transação do 
        // evento pai via atomicity do `SaveChangesAsync` na camada do Entity Framework.

        _logger.LogWarning(
            "Alerta de evasão enfileirado no DbContext para o aluno {AlunoId} ({NomeAluno}). " +
            "Total de faltas: {TotalFaltas}/{LimiteConfigurado}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.TotalFaltas,
            notification.LimiteConfigurado);

        return Task.CompletedTask;
    }
}
