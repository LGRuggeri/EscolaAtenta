using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Reflection;

namespace EscolaAtenta.API.Workers;

/// <summary>
/// Worker Service que envia sinais de "estou vivo" (Heartbeat) para a API na Nuvem.
///
/// Responsabilidades:
/// 1. Acordar a cada 15 minutos e coletar dados de saúde da instância local.
/// 2. Enviar POST com status do banco SQLite, fila de sincronização pendente e versão da API.
/// 3. Se a escola ficar offline, o servidor central detecta ausência de heartbeat
///    e pode alertar a equipe de suporte proativamente.
///
/// Configuração via appsettings.json na seção "Heartbeat".
/// Se o endpoint na nuvem não estiver configurado, o worker opera em modo silencioso (apenas loga).
/// </summary>
public class HeartbeatWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly TimeSpan _intervalo;

    public HeartbeatWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HeartbeatWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        var minutos = _configuration.GetValue("Heartbeat:IntervaloMinutos", 15);
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatWorker iniciado. Intervalo: {Intervalo} minutos.", _intervalo.TotalMinutes);

        // Aguarda 30 segundos após o startup para dar tempo de tudo inicializar
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = await ColetarDadosAsync(stoppingToken);
                await EnviarHeartbeatAsync(heartbeat, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao enviar heartbeat. Será tentado novamente no próximo ciclo.");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }

    private async Task<HeartbeatPayload> ColetarDadosAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Verifica se o banco está acessível
        var bancoOperacional = await dbContext.Database.CanConnectAsync(ct);

        // Conta registros de sincronização pendentes (sem data de sincronização recente)
        var syncPendentes = bancoOperacional
            ? await dbContext.SyncLogs.CountAsync(ct)
            : -1;

        var versaoApi = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "desconhecida";

        var processo = System.Diagnostics.Process.GetCurrentProcess();

        return new HeartbeatPayload
        {
            EscolaId = _configuration["Heartbeat:EscolaId"] ?? Environment.MachineName,
            Timestamp = DateTimeOffset.UtcNow,
            VersaoApi = versaoApi,
            BancoOperacional = bancoOperacional,
            SyncLogCount = syncPendentes,
            UsoMemoriaMb = processo.WorkingSet64 / (1024.0 * 1024.0),
            Uptime = DateTime.UtcNow - processo.StartTime.ToUniversalTime()
        };
    }

    private async Task EnviarHeartbeatAsync(HeartbeatPayload payload, CancellationToken ct)
    {
        var endpointNuvem = _configuration["Heartbeat:EndpointNuvem"];

        // Se não há endpoint configurado, apenas loga (modo desenvolvimento)
        if (string.IsNullOrWhiteSpace(endpointNuvem))
        {
            _logger.LogDebug(
                "Heartbeat coletado (modo local): Banco={BancoOk}, SyncLogs={SyncCount}, RAM={MemMb:F1}MB, Uptime={Uptime}",
                payload.BancoOperacional, payload.SyncLogCount, payload.UsoMemoriaMb, payload.Uptime);
            return;
        }

        var client = _httpClientFactory.CreateClient("Heartbeat");
        client.Timeout = TimeSpan.FromSeconds(10); // Evita bloqueio indefinido se nuvem estiver lenta

        var apiKey = _configuration["Heartbeat:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        var response = await client.PostAsJsonAsync(endpointNuvem, payload, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Heartbeat enviado com sucesso para {Endpoint}.", endpointNuvem);
        }
        else
        {
            _logger.LogWarning(
                "Heartbeat rejeitado pelo servidor: {StatusCode} {Reason}",
                (int)response.StatusCode, response.ReasonPhrase);
        }
    }
}

/// <summary>
/// Payload enviado ao servidor central com dados de saúde da instância local.
/// </summary>
public class HeartbeatPayload
{
    public required string EscolaId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string VersaoApi { get; init; }
    public bool BancoOperacional { get; init; }
    public int SyncLogCount { get; init; }
    public double UsoMemoriaMb { get; init; }
    public TimeSpan Uptime { get; init; }
}
