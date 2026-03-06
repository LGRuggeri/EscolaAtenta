using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EscolaAtenta.TrayMonitor.Services;

/// <summary>
/// Serviço de verificação de atualizações OTA (Over-The-Air) baseado em ficheiros estáticos (Serverless).
/// Faz polling no UpdateCheckUrl a cada 4 horas.
/// </summary>
public class UpdateCheckService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly System.Threading.Timer? _timer;
    private readonly string? _updateCheckUrl;
    private readonly Version _currentVersion;
    
    // Evento disparado quando há uma versão superior disponível
    public event Action<(string Version, string DownloadUrl)>? UpdateAvailable;

    public UpdateCheckService(string? updateCheckUrl)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _updateCheckUrl = updateCheckUrl;
        
        // Versão do Assembly atual
        var versionConfig = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = versionConfig ?? new Version(1, 0, 0, 0);

        if (!string.IsNullOrWhiteSpace(_updateCheckUrl))
        {
            // Checar a cada 4 horas (e iniciar a primeira verificação após 10 segundos)
            _timer = new System.Threading.Timer(VerificarAtualizacao, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(4));
        }
    }

    private async void VerificarAtualizacao(object? state)
    {
        if (string.IsNullOrWhiteSpace(_updateCheckUrl)) return;

        try
        {
            var jsonString = await _httpClient.GetStringAsync(_updateCheckUrl);
            var info = JsonSerializer.Deserialize<ReleaseInfo>(jsonString);

            if (info != null && Version.TryParse(info.Version, out var cloudVersion))
            {
                if (cloudVersion > _currentVersion)
                {
                    // Lança o evento na thread do chamador (TrayMonitor precisará invocar no Control)
                    UpdateAvailable?.Invoke((info.Version!, info.DownloadUrl!));
                    
                    // Se achou atualização, podemos parar o timer para não spammar
                    _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }
        catch (HttpRequestException) { /* Falha de rede: supressão silenciosa */ }
        catch (TaskCanceledException) { /* Timeout: supressão silenciosa */ }
        catch (JsonException) { /* JSON de atualização inválido: ignorar */ }
        catch (Exception) { /* Qualquer outro erro inesperado */ }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    // Estrutura esperada do version.json: { "version": "1.0.1", "downloadUrl": "https://..." }
    private class ReleaseInfo
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
        
        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }
    }
}
