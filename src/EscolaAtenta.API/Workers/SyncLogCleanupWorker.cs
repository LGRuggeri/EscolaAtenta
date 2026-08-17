using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.API.Workers;

/// <summary>
/// Worker que roda uma vez por dia.
///
/// IMPORTANTE: A limpeza automática de SyncLogs foi desativada.
/// SyncLogs são mapeamentos de identidade entre IDs locais do WatermelonDB
/// (alfanuméricos) e GUIDs do servidor. Eles são essenciais para que
/// ProcessarCreated, ObterChamadaPorTurmaEDiaHandler e SyncPullHandler
/// consigam resolver entidades criadas offline. Remover esses mapeamentos
/// após 90 dias causaria perda silenciosa de dados offline que nunca
/// chegaram ao servidor.
///
/// Se no futuro for necessário limpar SyncLogs, faça-o apenas para
/// mapeamentos cuja EntidadeId não exista mais no banco (órfãos reais),
/// nunca por data de sincronização.
/// </summary>
public class SyncLogCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncLogCleanupWorker> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    public SyncLogCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<SyncLogCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SYNCLOG-CLEANUP] Worker iniciado em modo desativado. Limpeza automática de SyncLogs não executada para preservar mapeamentos de identidade offline.");

        // Aguarda 5 minutos antes da primeira execução para não competir com o startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A limpeza automática de SyncLogs foi desativada.
                // Ver comentário da classe para justificativa.
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[SYNCLOG-CLEANUP] Erro durante limpeza.");
            }
        }
    }
}
