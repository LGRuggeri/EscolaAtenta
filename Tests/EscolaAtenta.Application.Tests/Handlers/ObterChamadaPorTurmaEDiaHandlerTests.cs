using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class ObterChamadaPorTurmaEDiaHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ObterChamadaPorTurmaEDiaHandlerTests()
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

    private static ObterChamadaPorTurmaEDiaHandler CriarHandler(AppDbContext ctx, FakeCurrentUserService? currentUser = null) =>
        new(ctx, currentUser ?? new FakeCurrentUserService());

    private static async Task VincularUsuarioTurma(AppDbContext ctx, Guid usuarioId, Guid turmaId)
    {
        ctx.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), usuarioId, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_QuandoChamadaExiste_DeveRetornarRegistrosEIndicarEdicao()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Consulta", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Consulta", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.PodeEditar.Should().BeTrue("chamada recente deve permitir edição");
        resultado.Registros.Should().HaveCount(1);
        resultado.Registros[0].AlunoId.Should().Be(alunoId.ToString());
        resultado.Registros[0].Status.Should().Be(StatusPresenca.Presente.ToString());
    }

    [Fact]
    public async Task Handle_QuandoChamadaNaoExiste_DeveRetornarNulo()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Vazia", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), new DateTime(2026, 1, 10)),
            CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ChamadaComMaisDe7Dias_DeveIndicarNaoEditavel()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Antiga", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Antigo", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Simula criação antiga
        var dataAntiga = DateTimeOffset.UtcNow.AddDays(-10);
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Chamadas SET DataCriacao = {dataAntiga} WHERE Id = {chamada.Id}");
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.PodeEditar.Should().BeFalse("chamada com mais de 7 dias não deve permitir edição");
    }

    // ── Testes IDOR ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MonitorSemVinculo_DeveNegarConsulta()
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
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Protegida", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Protegido", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx, fakeUser);
        Func<Task> act = () => handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*permissão*");
    }

    [Fact]
    public async Task Handle_MonitorComVinculo_DevePermitirConsulta()
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
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Vinculada", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Vinculado", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();

        await VincularUsuarioTurma(ctx, monitorId, turmaId);

        var handler = CriarHandler(ctx, fakeUser);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime),
            CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Registros.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_AdministradorSemVinculo_DevePermitirConsulta()
    {
        await using var ctx = CriarContexto(); // FakeCurrentUserService padrão = Administrador
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Admin", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Admin", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime),
            CancellationToken.None);

        resultado.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ComTurmaIdExterno_DeveResolverViaSyncLogERetornarChamada()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var idExternoTurma = "turma_offline_123";
        ctx.Turmas.Add(new Turma(turmaId, "Turma Offline", "Manhã", 2026));

        var alunoId = Guid.NewGuid();
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Offline", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Falta);
        ctx.Chamadas.Add(chamada);

        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = idExternoTurma,
            EntidadeId = turmaId,
            TabelaOrigem = "turmas",
            SincronizadoEm = DateTimeOffset.UtcNow
        });

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(idExternoTurma, data.DateTime),
            CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Registros.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ComAlunoIdExterno_DeveRetornarIdLocalNoDto()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var idExternoAluno = "aluno_offline_456";

        ctx.Turmas.Add(new Turma(turmaId, "Turma Sync", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Sync", null, turmaId));

        var data = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var chamada = new Chamada(Guid.NewGuid(), data, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, StatusPresenca.Atraso);
        ctx.Chamadas.Add(chamada);

        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = idExternoAluno,
            EntidadeId = alunoId,
            TabelaOrigem = "alunos",
            SincronizadoEm = DateTimeOffset.UtcNow
        });

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = CriarHandler(ctx);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId.ToString(), data.DateTime),
            CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Registros.Should().HaveCount(1);
        resultado.Registros[0].AlunoId.Should().Be(idExternoAluno);
    }
}
