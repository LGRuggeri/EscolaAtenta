using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

/// <summary>
/// Testes de integração que executam o pipeline real de MediatR + EF Core.
/// Objetivo: garantir que domain events de alerta não sejam duplicados quando
/// RecalcularEstatisticas e ReconciliarAlertasPendentes trabalham em sequência.
/// </summary>
public class RealizarChamadaHandlerIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RealizarChamadaHandlerIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private static ServiceProvider CriarServiceProvider(SqliteConnection connection, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        services.AddSingleton(currentUser);
        services.AddSingleton<IEscolaTenantProvider, FakeTenantProvider>();
        services.AddSingleton<ISqliteWriteLockProvider, FakeSqliteWriteLockProvider>();
        services.AddLogging();

        // MediatR real: registra todos os handlers do assembly de Application.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<RealizarChamadaHandler>());

        services.AddScoped<RealizarChamadaHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Handle_ChamadaComTresFaltasConsecutivas_DeveCriarApenasUmAlertaDeEvasao()
    {
        // Arrange
        var currentUser = new FakeCurrentUserService();
        await using var scope = CriarServiceProvider(_connection, currentUser).CreateAsyncScope();

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");

        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Integração", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Integração", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = scope.ServiceProvider.GetRequiredService<RealizarChamadaHandler>();

        for (int i = 0; i < 3; i++)
        {
            var cmd = new RealizarChamadaCommand(
                turmaId,
                Guid.NewGuid(),
                [new RegistroAlunoDto(alunoId, StatusPresenca.Falta)],
                new DateTimeOffset(2026, 1, 10 + i, 8, 0, 0, TimeSpan.Zero));

            await handler.Handle(cmd, CancellationToken.None);
            ctx.ChangeTracker.Clear();
        }

        // Assert
        var alertas = await ctx.AlertasEvasao
            .Where(a => a.AlunoId == alunoId && !a.Resolvido && a.Tipo == TipoAlerta.Evasao)
            .ToListAsync();

        alertas.Should().HaveCount(1, "deve haver exatamente um alerta de evasão pendente");
        alertas[0].Nivel.Should().Be(NivelAlertaFalta.Vermelho);
    }

    [Fact]
    public async Task Handle_ChamadaRetroativaComTresFaltas_DeveCriarApenasUmAlerta()
    {
        // Arrange
        var currentUser = new FakeCurrentUserService();
        await using var scope = CriarServiceProvider(_connection, currentUser).CreateAsyncScope();

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");

        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Retroativa", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Retroativo", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = scope.ServiceProvider.GetRequiredService<RealizarChamadaHandler>();

        for (int i = 0; i < 3; i++)
        {
            var cmd = new RealizarChamadaCommand(
                turmaId,
                Guid.NewGuid(),
                [new RegistroAlunoDto(alunoId, StatusPresenca.Falta)],
                new DateTimeOffset(2026, 1, 10 + i, 8, 0, 0, TimeSpan.Zero));

            await handler.Handle(cmd, CancellationToken.None);
            ctx.ChangeTracker.Clear();
        }

        // Assert
        var alertas = await ctx.AlertasEvasao
            .Where(a => a.AlunoId == alunoId && !a.Resolvido && a.Tipo == TipoAlerta.Evasao)
            .ToListAsync();

        alertas.Should().HaveCount(1, "chamada retroativa não deve duplicar alertas");
    }
}
