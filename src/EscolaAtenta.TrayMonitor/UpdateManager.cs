using System.Diagnostics;
using System.IO.Compression;
using System.ServiceProcess;

namespace EscolaAtenta.TrayMonitor;

/// <summary>
/// Gereção a rotina elevada (Administrador) de atualização OTA.
/// Executada quando o TrayMonitor é relançado da pasta %TEMP% com o argumento --update.
/// </summary>
public static class UpdateManager
{
    private const string NomeServico = "EscolaAtenta";
    private const string DiretorioInstalacao = @"C:\EscolaAtenta";

    public static async Task RunAsync(string downloadUrl)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "EscolaAtentaUpdate_" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempFolder, "release.zip");

        try
        {
            Directory.CreateDirectory(tempFolder);

            // 1. Download do zip
            Console.WriteLine("Fazendo download da nova versão...");
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            // 2. Parar o serviço Windows via ServiceController (Síncrono e Seguro)
            Console.WriteLine("Parando o serviço local...");
            using (var sc = new ServiceController(NomeServico))
            {
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                {
                    sc.Stop();
                }
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            // Um pequeno delay para libertar locks marginais do sistema operativo
            await Task.Delay(2000);

            // 3. Extração com Preservação Crítica de Dados
            Console.WriteLine("Extraindo atualização e preservando dados locais...");
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    // Ignora mapeamentos de diretórios vazios no zip
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var isLogsDir = entry.FullName.StartsWith("Logs/", StringComparison.OrdinalIgnoreCase) || 
                                    entry.FullName.StartsWith(@"Logs\", StringComparison.OrdinalIgnoreCase);
                    
                    var isAppSettings = entry.Name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                                        entry.Name.Equals("appsettings.Production.json", StringComparison.OrdinalIgnoreCase);
                                        
                    var isDatabase = entry.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                                     entry.Name.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase) ||
                                     entry.Name.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase);

                    // PÁRA CASO SEJA UM FICHEIRO SENSÍVEL QUE JÁ EXISTA NO DISCO
                    var destinationPath = Path.Combine(DiretorioInstalacao, entry.FullName);
                    
                    if ((isLogsDir || isAppSettings || isDatabase) && File.Exists(destinationPath))
                    {
                        Console.WriteLine($"[CRÍTICO] Preservando arquivo local: {entry.FullName}");
                        continue;
                    }

                    // Extrai com segurança criando a pasta de destino se for preciso
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            // 4. Iniciar o Serviço
            Console.WriteLine("Iniciando o serviço atualizado...");
            using (var sc = new ServiceController(NomeServico))
            {
                if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                {
                    sc.Start();
                }
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }

            // 5. Drop de Privilégios: Relançar o TrayMonitor original via Explorer (no path original)
            Console.WriteLine("Reiniciando TrayMonitor...");
            var originalTrayMonitorPath = Path.Combine(DiretorioInstalacao, "EscolaAtenta.TrayMonitor.exe");
            
            if (File.Exists(originalTrayMonitorPath))
            {
                // Iniciar via explorer força o processo filho a herdar o nível de privilégio do explorer (utente regular), não do updater.
                Process.Start("explorer.exe", $"\"{originalTrayMonitorPath}\"");
            }

            Console.WriteLine("Atualização concluída com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Falha crítica durante atualização OTA: {ex.Message}");
            // Em caso de falha silenciosa no updater temporário, um log num sítio agnóstico pode ser útil, mas por agora 
            // como ele roda invisível de background, podemos descartar as firebells para não alarmar.
        }
        finally
        {
            // Limpeza
            try
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
            }
            catch { /* Ignora locks de ficheiros zip pela task ainda estar finalizando */ }
        }
    }
}
