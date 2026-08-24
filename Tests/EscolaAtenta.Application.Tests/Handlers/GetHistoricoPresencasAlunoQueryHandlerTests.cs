using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetHistoricoPresencasAlunoQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetHistoricoPresencasAlunoQueryHandlerTests()
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

    private static GetHistoricoPresencasAlunoQueryHandler CriarHandler(AppDbContext ctx) =>
        new(ctx, new FakeCurrentUserService(), NullLogger<GetHistoricoPresencasAlunoQueryHandler>.Instance);

    /// <summary>
    /// Cria uma chamada com presença registrada em um único SaveChangesAsync,
    /// evitando o ConcurrencyException do SQLite ao salvar Chamada separadamente.
    /// </summary>
    private static async Task CriarChamadaComPresenca(
        AppDbContext ctx, Guid turmaId, Guid alunoId,
        DateTimeOffset dataHora, StatusPresenca status)
    {
        var chamada = new Chamada(Guid.NewGuid(), dataHora, turmaId, Guid.NewGuid());
        chamada.RegistrarPresenca(alunoId, status);
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_AlunoInexistente_DeveRetornarVazio()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(Guid.NewGuid().ToString()), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AlunoSemPresencas_DeveRetornarVazio()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Sem Presença", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(alunoId.ToString()), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComPresencas_DeveRetornarOrdenadoPorDataDesc()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Historico", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Historico", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow.AddDays(-1), StatusPresenca.Presente);
        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow, StatusPresenca.Falta);

        var resultado = (await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(alunoId.ToString(), Dias: 7), CancellationToken.None)).ToList();

        resultado.Should().HaveCount(2);
        resultado[0].DataDaChamada.Should().BeAfter(resultado[1].DataDaChamada);
        resultado[0].Status.Should().Be("Falta");
        resultado[1].Status.Should().Be("Presente");
    }

    [Fact]
    public async Task Handle_FiltroDataDias_DeveExcluirRegistrosAntigos()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Filtro", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Filtro", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow.AddDays(-1), StatusPresenca.Presente);
        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow.AddDays(-30), StatusPresenca.Falta);

        var resultado = (await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(alunoId.ToString(), Dias: 7), CancellationToken.None)).ToList();

        resultado.Should().HaveCount(1);
        resultado[0].Status.Should().Be("Presente");
    }

    [Fact]
    public async Task Handle_IdExternoWatermelonDb_DeveResolverViaSyncLog()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var idExterno = "watermelon_abc123";

        ctx.Turmas.Add(new Turma(turmaId, "Turma WM", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno WM", null, turmaId));
        ctx.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            IdExterno = idExterno,
            EntidadeId = alunoId,
            TabelaOrigem = "alunos",
            SincronizadoEm = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow, StatusPresenca.Presente);

        var resultado = (await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(idExterno), CancellationToken.None)).ToList();

        resultado.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_Padrao365Dias_DeveIncluirRegistrosAntigos()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Historico", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Historico", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow.AddDays(-1), StatusPresenca.Presente);
        await CriarChamadaComPresenca(ctx, turmaId, alunoId, DateTimeOffset.UtcNow.AddDays(-30), StatusPresenca.Falta);

        var resultado = (await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery(alunoId.ToString()), CancellationToken.None)).ToList();

        resultado.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_IdExternoInexistente_DeveRetornarVazio()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetHistoricoPresencasAlunoQuery("id_que_nao_existe"), CancellationToken.None);

        resultado.Should().BeEmpty();
    }
}
