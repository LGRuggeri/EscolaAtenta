using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.EventHandlers;

/// <summary>
/// Handler para o Domain Event AtrasosTrimestreNormalizadosEvent.
/// 
/// Responsabilidade: Resolver automaticamente alertas de atraso pendentes
/// quando o recálculo do histórico de presenças deixa o aluno com menos de
/// 3 atrasos no trimestre (por exemplo, após correção de um atraso).
/// </summary>
public class AtrasosTrimestreNormalizadosHandler : INotificationHandler<AtrasosTrimestreNormalizadosEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<AtrasosTrimestreNormalizadosHandler> _logger;

    public AtrasosTrimestreNormalizadosHandler(
        AppDbContext context,
        ILogger<AtrasosTrimestreNormalizadosHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        AtrasosTrimestreNormalizadosEvent notification,
        CancellationToken cancellationToken)
    {
        var alertaPendente = await _context.AlertasEvasao
            .FirstOrDefaultAsync(a => a.AlunoId == notification.AlunoId
                                   && !a.Resolvido
                                   && a.Tipo == TipoAlerta.Atraso, cancellationToken);

        if (alertaPendente is null)
            return;

        alertaPendente.ResolverAutomaticamente(
            "Alerta resolvido automaticamente após correção de presença: atrasos do trimestre normalizados.");

        _logger.LogInformation(
            "Alerta de atraso resolvido automaticamente para o aluno {AlunoId} ({NomeAluno}). " +
            "Atrasos no trimestre: {AtrasosNoTrimestre}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.AtrasosNoTrimestre);
    }
}
