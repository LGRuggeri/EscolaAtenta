using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

/// <summary>
/// Testes do SyncPullHandler — protocolo WatermelonDB Sync.
///
/// NOTA: O delta sync (lastPulledAt > 0) usa DataCriacao.UtcTicks para filtrar no SQLite WAL,
/// mas o provider in-memory do EF Core não suporta essa tradução LINQ. Por isso, os testes
/// cobrem o fluxo de primeiro sync (lastPulledAt == 0) e mapeamento SyncLog. O delta sync
/// é validado via testes de integração com o SQLite real em produção.
/// </summary>
public class SyncPullHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SyncPullHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CriarContexto()
    {
        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private static SyncPullHandler CriarHandler(AppDbContext ctx) =>
        new(ctx, NullLogger<SyncPullHandler>.Instance);

    [Fact]
    public async Task Handle_PrimeiroSync_DeveRetornarTudoEmCreated()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "1º Ano", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "João", null, turmaId));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Maria", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        resultado.Timestamp.Should().BeGreaterThan(0);
        resultado.Changes.Turmas.Created.Should().HaveCount(1);
        resultado.Changes.Turmas.Updated.Should().BeEmpty();
        resultado.Changes.Turmas.Deleted.Should().BeEmpty();
        resultado.Changes.Alunos.Created.Should().HaveCount(2);
        resultado.Changes.Alunos.Updated.Should().BeEmpty();
        resultado.Changes.Alunos.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PrimeiroSync_LastPulledAtNull_DeveRetornarTudo()
    {
        await using var ctx = CriarContexto();
        ctx.Turmas.Add(new Turma(Guid.NewGuid(), "2º Ano", "Tarde", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: null), CancellationToken.None);

        resultado.Changes.Turmas.Created.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PrimeiroSync_DeveMapearCamposDoDto()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "3º Ano B", "Tarde", 2026));
        var alunoId = Guid.NewGuid();
        ctx.Alunos.Add(new Aluno(alunoId, "Carlos", "MAT-001", turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        var turmaDto = resultado.Changes.Turmas.Created.First();
        turmaDto.Nome.Should().Be("3º Ano B");
        turmaDto.Turno.Should().Be("Tarde");
        turmaDto.AnoLetivo.Should().Be(2026);

        var alunoDto = resultado.Changes.Alunos.Created.First();
        alunoDto.Nome.Should().Be("Carlos");
        alunoDto.FaltasConsecutivasAtuais.Should().Be(0);
        alunoDto.TotalFaltas.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SyncLogExistente_DeveUsarIdLocalNoPayload()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "4º Ano", "Manhã", 2026));
        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = "local-watermelon-id",
            EntidadeId = turmaId,
            TabelaOrigem = "turmas",
            SincronizadoEm = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        resultado.Changes.Turmas.Created.Should().ContainSingle()
            .Which.Id.Should().Be("local-watermelon-id",
                "deve usar o ID local do WatermelonDB ao invés do Guid do servidor");
    }

    [Fact]
    public async Task Handle_SyncLogAluno_DeveResolverTurmaIdLocal()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Ana", null, turmaId));
        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = "wm-turma-local",
            EntidadeId = turmaId,
            TabelaOrigem = "turmas",
            SincronizadoEm = DateTimeOffset.UtcNow
        });
        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = "wm-aluno-local",
            EntidadeId = alunoId,
            TabelaOrigem = "alunos",
            SincronizadoEm = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        var alunoDto = resultado.Changes.Alunos.Created.First();
        alunoDto.Id.Should().Be("wm-aluno-local");
        alunoDto.TurmaId.Should().Be("wm-turma-local",
            "TurmaId no payload deve usar o ID local do WatermelonDB");
    }

    [Fact]
    public async Task Handle_BancoVazio_DeveRetornarListasVazias()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        resultado.Changes.Turmas.Created.Should().BeEmpty();
        resultado.Changes.Alunos.Created.Should().BeEmpty();
        resultado.Timestamp.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_PrimeiroSync_RegistrosPresencaSempreVazio()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "6º Ano", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new SyncPullQuery(LastPulledAt: 0), CancellationToken.None);

        resultado.Changes.RegistrosPresenca.Created.Should().BeEmpty();
        resultado.Changes.RegistrosPresenca.Updated.Should().BeEmpty();
        resultado.Changes.RegistrosPresenca.Deleted.Should().BeEmpty();
    }
}
