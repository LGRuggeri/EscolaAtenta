using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.API.Workers;

/// <summary>
/// Worker background responsável por sincronizar dados locais com a Nuvem (Multi-Tenant).
/// Parte da estratégia "Design for Scale, Build for Now".
/// 
/// Está atualmente protegido por uma Feature Toggle e inicializa em "Standby Mode" 
/// para poupar recursos nas máquinas das escolas na fase atual de Single-School.
/// </summary>
public class CloudEgressWorker : BackgroundService
{
    private readonly ILogger<CloudEgressWorker> _logger;
    private readonly bool _enabled;
    private readonly int _intervalMinutes;

    public CloudEgressWorker(ILogger<CloudEgressWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        _enabled = configuration.GetValue<bool>("CloudSync:Enabled", false);
        _intervalMinutes = configuration.GetValue<int>("CloudSync:IntervalMinutes", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Cloud Sync desativado por Feature Toggle. Worker de egressão adormecido.");
            // Suspende o worker indefinidamente sem consumir CPU
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        _logger.LogInformation("Cloud Sync Worker iniciado. Verificando dados para envio a cada {Intervalo} minutos.", _intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // TODO: No futuro, criar um scope, carregar entidades com CloudSyncedAt == null e enviar para o endpoint da nuvem.
                _logger.LogInformation("Procurando registros não sincronizados...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ciclo do CloudEgressWorker.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }
    }
}
