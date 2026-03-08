using System.Diagnostics;
using System.IO.Compression;
using System.Security;
using System.ServiceProcess;

namespace EscolaAtenta.TrayMonitor;

/// <summary>
/// Gereção a rotina elevada (Administrador) de atualização OTA com blindagem DevSecOps.
/// Executada quando o TrayMonitor é relançado da pasta %TEMP% com o argumento --update.
/// </summary>
public static class UpdateManager
{
    private const string NomeServico = "EscolaAtenta";

    public static async Task RunAsync(string downloadUrl)
    {
        // Forçar HTTPS estrito como política de segurança básica (Previne MITM amador)
        if (!downloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new SecurityException("OTA Updates só operam sobre canais seguros (HTTPS).");

        var tempFolder = Path.Combine(Path.GetTempPath(), "EscolaAtentaUpdate_" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempFolder, "release.zip");
        
        // Determina dinamicamente o diretório da aplicação em vez de usar hardcoded C:\
        var diretorioInstalacao = AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            Directory.CreateDirectory(tempFolder);

            Console.WriteLine("Fazendo download da nova versão...");
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                var response = await httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }

            Console.WriteLine("Parando o serviço local...");
            using (var sc = new ServiceController(NomeServico))
            {
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    sc.Stop();
                    
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            await Task.Delay(2000); // Libertar locks marginais OS

            Console.WriteLine("Extraindo atualização com blindagem ZipSlip e preservação de dados...");
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    // DEFESA 1: Zip Slip (Impede ficheiros com path relative attack ex: ../../Windows)
                    var destinationPath = Path.GetFullPath(Path.Combine(diretorioInstalacao, entry.FullName));
                    if (!destinationPath.StartsWith(diretorioInstalacao, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Tentativa de Path Traversal (Zip Slip) detetada. Atualização abortada.");

                    var isLogsDir = destinationPath.Contains(@"\Logs\") || destinationPath.Contains(@"/Logs/");
                    var isConfig = entry.Name.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                                   entry.Name.EndsWith("appsettings.Production.json", StringComparison.OrdinalIgnoreCase);
                    var isDb = entry.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                               entry.Name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
                               entry.Name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase);

                    // DEFESA 2: Preservação de Dados de Produção
                    if ((isLogsDir || isConfig || isDb) && File.Exists(destinationPath))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            Console.WriteLine("Iniciando o serviço atualizado...");
            using (var sc = new ServiceController(NomeServico))
            {
                if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                    sc.Start();
                    
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }

            Console.WriteLine("Reiniciando TrayMonitor no contexto do utilizador...");
            var originalTrayMonitorPath = Path.Combine(diretorioInstalacao, "EscolaAtenta.TrayMonitor.exe");
            if (File.Exists(originalTrayMonitorPath))
            {
                // Iniciar via explorer força drop de privilégios de Admin para Standard User
                Process.Start("explorer.exe", $"\"{originalTrayMonitorPath}\"");
            }
        }
        catch (Exception ex)
        {
            // O ideal seria logar no EventViewer do Windows em vez de apenas Console
            EventLog.WriteEntry("Application", $"Falha OTA Update EscolaAtenta: {ex.Message}", EventLogEntryType.Error);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }
}
