using EscolaAtenta.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.API;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // Proteção crítica: DataSeeder apaga todos os dados — NUNCA executar em produção
        if (env.IsProduction())
        {
            logger.LogWarning("[DATASEED] Execução bloqueada em ambiente de Produção. Dados preservados.");
            return;
        }

        try
        {
            logger.LogInformation("Iniciando limpeza do banco de dados...");

            // Limpa dados transacionais
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"AlertasEvasao\" CASCADE;");
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"RegistrosPresenca\" CASCADE;");
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Chamadas\" CASCADE;");
            
            // Limpa dados de cadastro secundário
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Alunos\" CASCADE;");
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Turmas\" CASCADE;");
            
            // Mantém apenas os usuários administradores (se houver regra específica detalhe aqui, 
            // mas o truncate no cascade de alunos já resolve a maioria das constraints)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Usuarios\" WHERE \"Papel\" != 3;"); // 3 = Administrador

            logger.LogInformation("Banco de dados limpo com sucesso. Apenas Administradores mantidos.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao limpar o banco de dados.");
        }
    }
}
