using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.EventHandlers;

/// <summary>
/// Handler para o Domain Event FaltasConsecutivasNormalizadasEvent.
/// 
/// Responsabilidade: Resolver automaticamente alertas de evasão pendentes
/// quando o recálculo do histórico de presenças zera as faltas consecutivas
/// do aluno (por exemplo, após correção de uma falta para presença).
/// </summary>
public class FaltasConsecutivasNormalizadasHandler : INotificationHandler<FaltasConsecutivasNormalizadasEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<FaltasConsecutivasNormalizadasHandler> _logger;

    public FaltasConsecutivasNormalizadasHandler(
        AppDbContext context,
        ILogger<FaltasConsecutivasNormalizadasHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        FaltasConsecutivasNormalizadasEvent notification,
        CancellationToken cancellationToken)
    {
        var alertaPendente = await _context.AlertasEvasao
            .FirstOrDefaultAsync(a => a.AlunoId == notification.AlunoId
                                   && !a.Resolvido
                                   && a.Tipo == TipoAlerta.Evasao, cancellationToken);

        if (alertaPendente is null)
            return;

        alertaPendente.ResolverAutomaticamente(
            "Alerta resolvido automaticamente após correção de presença: faltas consecutivas normalizadas.");

        _logger.LogInformation(
            "Alerta de evasão resolvido automaticamente para o aluno {AlunoId} ({NomeAluno}). " +
            "Faltas consecutivas atuais: {FaltasConsecutivasAtuais}.",
            notification.AlunoId,
            notification.NomeAluno,
            notification.FaltasConsecutivasAtuais);
    }
}
