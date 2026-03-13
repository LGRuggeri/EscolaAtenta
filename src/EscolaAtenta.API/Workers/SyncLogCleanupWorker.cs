using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.API.Workers;

/// <summary>
/// Worker que roda uma vez por dia e remove SyncLogs com mais de 90 dias.
/// SyncLogs são registros de mapeamento WatermelonDB ID → GUID do servidor.
/// Após 90 dias, um registro não sincronizado provavelmente não voltará mais,
/// e manter o histórico infinito degrada performance das queries de sync.
/// </summary>
public class SyncLogCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncLogCleanupWorker> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);
    private static readonly TimeSpan Retencao = TimeSpan.FromDays(90);

    public SyncLogCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<SyncLogCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SYNCLOG-CLEANUP] Worker iniciado. Limpeza a cada {Horas}h, retencao de {Dias} dias.",
            Intervalo.TotalHours, Retencao.TotalDays);

        // Aguarda 5 minutos antes da primeira execução para não competir com o startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var limite = DateTimeOffset.UtcNow - Retencao;
                var removidos = await context.SyncLogs
                    .Where(s => s.SincronizadoEm < limite)
                    .ExecuteDeleteAsync(stoppingToken);

                if (removidos > 0)
                    _logger.LogInformation("[SYNCLOG-CLEANUP] {Count} registros antigos removidos.", removidos);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[SYNCLOG-CLEANUP] Erro durante limpeza.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
