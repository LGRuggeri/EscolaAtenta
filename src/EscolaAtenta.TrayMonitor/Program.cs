namespace EscolaAtenta.TrayMonitor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Interceta rotina de atualização (via RunAs UAC a partir do executável temporário)
        if (args.Length >= 2 && args[0] == "--update")
        {
            var updateUrl = args[1];
            UpdateManager.RunAsync(updateUrl).GetAwaiter().GetResult();
            return; // Encerra o updater sem abrir UI ou trancar Mutex
        }

        // Garante apenas uma instância do Tray App rodando (para o fluxo normal)
        using var mutex = new Mutex(true, "EscolaAtenta.TrayMonitor", out var instanciaUnica);
        if (!instanciaUnica)
        {
            MessageBox.Show(
                "O Monitor EscolaAtenta já está em execução na bandeja do sistema.",
                "EscolaAtenta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
