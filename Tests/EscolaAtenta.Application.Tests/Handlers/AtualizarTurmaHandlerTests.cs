using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class AtualizarTurmaHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AtualizarTurmaHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CriarContexto(FakeCurrentUserService? currentUser = null)
    {
        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options,
            currentUser ?? new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private static AtualizarTurmaHandler CriarHandler(AppDbContext ctx, FakeCurrentUserService? currentUser = null) =>
        new(ctx, currentUser ?? new FakeCurrentUserService(), NullLogger<AtualizarTurmaHandler>.Instance);

    [Fact]
    public async Task Handle_TurmaInexistente_DeveLancarKeyNotFoundException()
    {
        await using var ctx = CriarContexto();

        var command = new AtualizarTurmaCommand(Guid.NewGuid(), "Nome", "Manhã", 2026);

        Func<Task> act = () => CriarHandler(ctx).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_ComandoValido_DeveAtualizarTurma()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Nome Antigo", "Manhã", 2025));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarTurmaCommand(turmaId, "Nome Novo", "Tarde", 2026);
        await CriarHandler(ctx).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var turma = await ctx.Turmas.FindAsync(turmaId);
        turma!.Nome.Should().Be("Nome Novo");
        turma.Turno.Should().Be("Tarde");
        turma.AnoLetivo.Should().Be(2026);
    }

    [Fact]
    public async Task Handle_MonitorSemVinculo_DeveLancarKeyNotFoundException()
    {
        var monitorId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = monitorId.ToString(),
            EstaAutenticado = true,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarTurmaCommand(turmaId, "Novo Nome", "Tarde", 2026);

        Func<Task> act = () => CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_MonitorComVinculo_DevePermitirAtualizacao()
    {
        var monitorId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = monitorId.ToString(),
            EstaAutenticado = true,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Original", "Manhã", 2026));
        ctx.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), monitorId, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarTurmaCommand(turmaId, "Turma Atualizada", "Tarde", 2026);
        await CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var turma = await ctx.Turmas.FindAsync(turmaId);
        turma!.Nome.Should().Be("Turma Atualizada");
    }

    [Fact]
    public async Task Handle_AdministradorSemVinculo_DevePermitirAtualizacao()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Admin", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarTurmaCommand(turmaId, "Turma Admin Atualizada", "Noite", 2026);
        await CriarHandler(ctx).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var turma = await ctx.Turmas.FindAsync(turmaId);
        turma!.Nome.Should().Be("Turma Admin Atualizada");
    }
}
