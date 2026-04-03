using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetAlunosComFaltasHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetAlunosComFaltasHandlerTests()
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

    private static GetAlunosComFaltasHandler CriarHandler(AppDbContext ctx) => new(ctx);

    private async Task<(Guid turmaId, Guid alunoId)> SeedAlunoComFaltas(
        AppDbContext ctx, string nome, int totalFaltas, int faltasConsecutivas, Guid? turmaId = null)
    {
        var tid = turmaId ?? Guid.NewGuid();
        if (!await ctx.Turmas.AnyAsync(t => t.Id == tid))
        {
            ctx.Turmas.Add(new Turma(tid, "Turma Teste", "Manhã", 2026));
        }

        var aluno = new Aluno(Guid.NewGuid(), nome, null, tid);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        // Registra faltas via chamadas para incrementar contadores na entidade
        for (int i = 0; i < totalFaltas; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-totalFaltas + i), tid, Guid.NewGuid());
            ctx.Chamadas.Add(chamada);
            await ctx.SaveChangesAsync();

            // Usar o contexto para atualizar via domínio
            var alunoDb = await ctx.Alunos.FindAsync(aluno.Id);
            alunoDb!.RegistrarPresenca(StatusPresenca.Falta, chamada.DataHora.UtcDateTime);
            await ctx.SaveChangesAsync();
            ctx.ChangeTracker.Clear();
        }

        return (tid, aluno.Id);
    }

    [Fact]
    public async Task Handle_SemAlunos_DeveRetornarListaVazia()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComAlunos_DeveRetornarOrdenadoPorTotalFaltasDesc()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Faltas", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Poucos Faltas", "MAT001", turmaId));
        await ctx.SaveChangesAsync();

        // Aluno com 3 faltas
        await SeedAlunoComFaltas(ctx, "Muitas Faltas", 3, 3, turmaId);

        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(), CancellationToken.None);

        resultado.Should().HaveCountGreaterOrEqualTo(2);
        resultado.First().Nome.Should().Be("Muitas Faltas");
    }

    [Fact]
    public async Task Handle_FiltroTurmaId_DeveRetornarApenasDaTurma()
    {
        await using var ctx = CriarContexto();
        var turmaA = Guid.NewGuid();
        var turmaB = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaA, "Turma A", "Manhã", 2026));
        ctx.Turmas.Add(new Turma(turmaB, "Turma B", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno A", null, turmaA));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno B", null, turmaB));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(turmaA), CancellationToken.None);

        resultado.Should().HaveCount(1);
        resultado.First().Nome.Should().Be("Aluno A");
    }

    [Fact]
    public async Task Handle_NivelAlerta_DeveCalcularCorretamente()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Nivel", "Manhã", 2026));

        // Aluno sem faltas consecutivas → Nenhum
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno Zero", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(), CancellationToken.None);

        var alunoZero = resultado.First(a => a.Nome == "Aluno Zero");
        alunoZero.NivelAlerta.Should().Be("Nenhum");
    }

    [Fact]
    public async Task Handle_DeveMapearNomeTurma()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano B", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno DTO", "MAT-999", turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(), CancellationToken.None);

        var dto = resultado.Single();
        dto.NomeTurma.Should().Be("5º Ano B");
        dto.Matricula.Should().Be("MAT-999");
    }

    [Fact]
    public async Task Handle_SemTurmaId_DeveRetornarTodasTurmas()
    {
        await using var ctx = CriarContexto();
        var turmaA = Guid.NewGuid();
        var turmaB = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaA, "Turma A", "Manhã", 2026));
        ctx.Turmas.Add(new Turma(turmaB, "Turma B", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno A", null, turmaA));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno B", null, turmaB));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosComFaltasQuery(), CancellationToken.None);

        resultado.Should().HaveCount(2);
    }
}
