using EscolaAtenta.Application.Dashboard.Handlers;
using EscolaAtenta.Application.Dashboard.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

/// <summary>
/// Usa SQLite in-memory para garantir que a query do handler seja traduzível
/// pelo provider real usado em produção.
/// </summary>
public class GetTurmasFrequenciaPerfeitaQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetTurmasFrequenciaPerfeitaQueryHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static GetTurmasFrequenciaPerfeitaQueryHandler CriarHandler(AppDbContext ctx) => new(ctx);

    private static GetTurmasFrequenciaPerfeitaQuery QueryMesAtual() =>
        new(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc));

    private static async Task SeedChamadaComPresenca(
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
    public async Task Handle_SemChamadas_DeveRetornarVazio()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        ctx.Turmas.Add(new Turma(Guid.NewGuid(), "Turma Sem Chamadas", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TurmaComApenasPresentesNoPeriodo_DeveRetornar()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Perfeita", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Exemplar", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Presente);

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().HaveCount(1);
        var dto = resultado.First();
        dto.NomeTurma.Should().Be("Turma Perfeita");
        dto.QuantidadeAulasMinistradas.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TurmaComFaltaNoPeriodo_NaoDeveRetornar()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Com Falta", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Faltoso", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Falta);

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TurmaComAtrasoNoPeriodo_NaoDeveRetornar()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Com Atraso", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Atrasado", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Atraso);

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ChamadaForaDoPeriodo_NaoDeveContar()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Fora Periodo", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Presente);

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ChamadasDentroEForaDoPeriodo_DeveContarApenasDentro()
    {
        await using var ctx = CriarContexto();
        ctx.Database.EnsureCreated();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Mista", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Fora do período (fevereiro)
        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Presente);

        // Dentro do período (março)
        await SeedChamadaComPresenca(ctx, turmaId, alunoId,
            new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero), StatusPresenca.Presente);

        var resultado = await CriarHandler(ctx).Handle(QueryMesAtual(), CancellationToken.None);

        resultado.Should().ContainSingle();
        var dto = resultado.First();
        dto.NomeTurma.Should().Be("Turma Mista");
        dto.QuantidadeAulasMinistradas.Should().Be(1);
    }
}
