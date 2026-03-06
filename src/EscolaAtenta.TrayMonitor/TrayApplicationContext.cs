using System.Diagnostics;
using System.Net.Http.Json;
using System.ServiceProcess;
using EscolaAtenta.TrayMonitor.Services;
using Microsoft.Extensions.Configuration;

namespace EscolaAtenta.TrayMonitor;

/// <summary>
/// ApplicationContext que gerencia o ícone da bandeja do sistema (System Tray).
///
/// Funcionalidades:
/// 1. Indicador visual de saúde (verde/amarelo/vermelho) via ícone dinâmico.
/// 2. Polling do endpoint /health da API local a cada 30 segundos.
/// 3. Menu de contexto com ações: Abrir Sistema, Ver Logs, Reiniciar Serviço, Sair.
/// 4. Controle do ciclo de vida do Serviço do Windows "EscolaAtenta".
/// 5. OTA Updates (Serverless via UpdateCheckService).
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private const string NomeServico = "EscolaAtenta";
    private const string UrlHealthPadrao = "http://localhost:5114/health";
    private const int IntervaloPollingMs = 30_000;

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HttpClient _httpClient;
    private readonly ToolStripMenuItem _menuStatus;
    private readonly ToolStripMenuItem _menuInstalarUpdate;
    private readonly UpdateCheckService _updateCheckService;
    private readonly SynchronizationContext? _syncContext;

    private enum StatusSaude { Desconhecido, Operacional, Degradado, Falha }
    private StatusSaude _statusAtual = StatusSaude.Desconhecido;
    private string? _pendingUpdateUrl;

    public TrayApplicationContext()
    {
        _syncContext = SynchronizationContext.Current;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Configuração
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
            
        var updateUrl = config["UpdateCheckUrl"];

        // Menu de contexto
        _menuStatus = new ToolStripMenuItem("Status: Verificando...") { Enabled = false };
        _menuInstalarUpdate = new ToolStripMenuItem("Instalar Atualização", null, OnInstalarUpdate)
        {
            Visible = false,
            BackColor = Color.LightCyan,
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };

        var menuAbrir = new ToolStripMenuItem("Abrir Sistema", null, OnAbrirSistema);
        var menuLogs = new ToolStripMenuItem("Ver Logs", null, OnVerLogs);
        var menuReiniciar = new ToolStripMenuItem("Reiniciar Serviço", null, OnReiniciarServico);
        var menuSair = new ToolStripMenuItem("Sair", null, OnSair);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_menuStatus);
        contextMenu.Items.Add(_menuInstalarUpdate);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(menuAbrir);
        contextMenu.Items.Add(menuLogs);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(menuReiniciar);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(menuSair);

        // NotifyIcon — ícone na bandeja do sistema
        _notifyIcon = new NotifyIcon
        {
            Icon = CriarIcone(Color.Gray),
            Text = "EscolaAtenta — Verificando...",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += OnAbrirSistema;

        // Timer de polling do health
        _timer = new System.Windows.Forms.Timer { Interval = IntervaloPollingMs };
        _timer.Tick += async (_, _) => await VerificarSaudeAsync();
        _timer.Start();

        // Inicialização do Update Checker
        _updateCheckService = new UpdateCheckService(updateUrl);
        _updateCheckService.UpdateAvailable += args => 
        {
            if (_syncContext != null)
                _syncContext.Post(_ => ShowUpdateAlert(args.Version, args.DownloadUrl), null);
            else
                ShowUpdateAlert(args.Version, args.DownloadUrl);
        };

        // Primeira verificação imediata
        _ = VerificarSaudeAsync();
    }

    private void ShowUpdateAlert(string version, string downloadUrl)
    {
        _pendingUpdateUrl = downloadUrl;
        _menuInstalarUpdate.Text = $"Instalar Atualização ({version})";
        _menuInstalarUpdate.Visible = true;
        
        // Altera ícone para destacar o update
        _notifyIcon.Icon = CriarIcone(Color.DeepSkyBlue);
        
        _notifyIcon.ShowBalloonTip(
            10000, "EscolaAtenta Update",
            $"Uma nova versão ({version}) está disponível. Clique com o botão direito e selecione 'Instalar Atualização'.",
            ToolTipIcon.Info);
    }

    private void OnInstalarUpdate(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_pendingUpdateUrl)) return;

        var currentExe = Application.ExecutablePath;
        var tempExeFileName = "EscolaAtentaUpdater.exe";
        var tempExePath = Path.Combine(Path.GetTempPath(), tempExeFileName);

        try
        {
            // Workaround para File in Use (Lock): O TrayMonitor não pode atualizar o próprio .exe 
            // enquanto está a correr fora da pasta gerida. Então, ele copia a si mesmo para o %TEMP%.
            File.Copy(currentExe, tempExePath, true);

            var psi = new ProcessStartInfo
            {
                FileName = tempExePath,
                Arguments = $"--update \"{_pendingUpdateUrl}\"",
                UseShellExecute = true,
                Verb = "runas" // Solicita elevação do UAC para a rotina de atualização
            };

            Process.Start(psi);
            
            // Suicídio guiado para libertar os locks do diretório C:\EscolaAtenta
            OnSair(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao iniciar a atualização: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task VerificarSaudeAsync()
    {
        // Se há update pendente, preserva a interface de atualização (azul)
        if (!string.IsNullOrWhiteSpace(_pendingUpdateUrl)) return;

        try
        {
            var response = await _httpClient.GetAsync(UrlHealthPadrao);

            if (response.IsSuccessStatusCode)
            {
                var dados = await response.Content.ReadFromJsonAsync<HealthResponse>();
                var novoStatus = dados?.Status?.ToLowerInvariant() switch
                {
                    "healthy" => StatusSaude.Operacional,
                    "degraded" => StatusSaude.Degradado,
                    _ => StatusSaude.Falha
                };
                AtualizarStatus(novoStatus, dados?.Status ?? "OK");
            }
            else
            {
                AtualizarStatus(StatusSaude.Falha, $"HTTP {(int)response.StatusCode}");
            }
        }
        catch (HttpRequestException)
        {
            AtualizarStatus(StatusSaude.Falha, "Serviço não encontrado");
        }
        catch (TaskCanceledException)
        {
            AtualizarStatus(StatusSaude.Falha, "Timeout na conexão");
        }
    }

    private void AtualizarStatus(StatusSaude status, string detalhe)
    {
        if (_statusAtual == status) return;
        _statusAtual = status;

        var (cor, texto, tooltip) = status switch
        {
            StatusSaude.Operacional => (Color.LimeGreen, "Operacional", "EscolaAtenta — Operacional"),
            StatusSaude.Degradado  => (Color.Gold, $"Degradado: {detalhe}", "EscolaAtenta — Degradado"),
            StatusSaude.Falha      => (Color.Red, $"Falha: {detalhe}", "EscolaAtenta — Serviço Parado"),
            _                      => (Color.Gray, "Verificando...", "EscolaAtenta — Verificando...")
        };

        _notifyIcon.Icon = CriarIcone(cor);
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        _menuStatus.Text = $"Status: {texto}";

        // Notificação balloon ao mudar de status
        if (status == StatusSaude.Falha)
        {
            _notifyIcon.ShowBalloonTip(
                5000, "EscolaAtenta",
                "O serviço de frequência parou. Clique com o botão direito para reiniciar.",
                ToolTipIcon.Error);
        }
        else if (status == StatusSaude.Operacional && _statusAtual != StatusSaude.Desconhecido)
        {
            _notifyIcon.ShowBalloonTip(
                3000, "EscolaAtenta",
                "Serviço de frequência está operacional.",
                ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// Cria um ícone circular colorido programaticamente (sem arquivo .ico externo).
    /// </summary>
    private static Icon CriarIcone(Color cor)
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(cor);
        g.FillEllipse(brush, 1, 1, 14, 14);
        // Borda para visibilidade em fundos claros
        using var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1);
        g.DrawEllipse(pen, 1, 1, 14, 14);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnAbrirSistema(object? sender, EventArgs e)
    {
        // Abre a interface web da API local no navegador padrão
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5114/health",
                UseShellExecute = true
            });
        }
        catch { /* Navegador não encontrado — ignora silenciosamente */ }
    }

    private void OnVerLogs(object? sender, EventArgs e)
    {
        // Abre a pasta de logs no Explorer
        var logPath = Path.Combine(AppContext.BaseDirectory, "Logs");

        // Tenta o caminho padrão de produção primeiro
        var logPathProd = @"C:\EscolaAtenta\Logs";
        if (Directory.Exists(logPathProd))
            logPath = logPathProd;

        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = logPath,
            UseShellExecute = true
        });
    }

    private void OnReiniciarServico(object? sender, EventArgs e)
    {
        try
        {
            var resultado = MessageBox.Show(
                "Deseja reiniciar o serviço de frequência?",
                "EscolaAtenta — Reiniciar Serviço",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (resultado != DialogResult.Yes) return;

            // Tenta reiniciar via sc.exe (não requer referência ao ServiceController)
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop {NomeServico}",
                UseShellExecute = true,
                Verb = "runas", // Solicita elevação (UAC)
                CreateNoWindow = true
            };

            Process.Start(psi)?.WaitForExit(10_000);
            Task.Delay(2000).Wait();

            psi.Arguments = $"start {NomeServico}";
            Process.Start(psi)?.WaitForExit(10_000);

            _notifyIcon.ShowBalloonTip(3000, "EscolaAtenta",
                "Serviço reiniciado. Aguarde a verificação...", ToolTipIcon.Info);

            // Força reverificação após 5 segundos
            _ = Task.Delay(5000).ContinueWith(_ => VerificarSaudeAsync());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erro ao reiniciar o serviço:\n{ex.Message}\n\nVerifique se você tem permissão de administrador.",
                "EscolaAtenta — Erro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnSair(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _httpClient.Dispose();
        _updateCheckService.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
            _httpClient.Dispose();
            _updateCheckService.Dispose();
        }
        base.Dispose(disposing);
    }

    private record HealthResponse(string? Status, string? TotalDuration);
}
