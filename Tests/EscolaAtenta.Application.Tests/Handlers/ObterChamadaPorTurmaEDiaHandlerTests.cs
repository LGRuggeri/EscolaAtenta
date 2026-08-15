using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
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

        var handler = new ObterChamadaPorTurmaEDiaHandler(ctx);
        var resultado = await handler.Handle(new ObterChamadaPorTurmaEDiaQuery(turmaId, data.DateTime), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.PodeEditar.Should().BeTrue("chamada recente deve permitir edição");
        resultado.Registros.Should().HaveCount(1);
        resultado.Registros[0].AlunoId.Should().Be(alunoId);
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

        var handler = new ObterChamadaPorTurmaEDiaHandler(ctx);
        var resultado = await handler.Handle(
            new ObterChamadaPorTurmaEDiaQuery(turmaId, new DateTime(2026, 1, 10)),
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

        var handler = new ObterChamadaPorTurmaEDiaHandler(ctx);
        var resultado = await handler.Handle(new ObterChamadaPorTurmaEDiaQuery(turmaId, data.DateTime), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.PodeEditar.Should().BeFalse("chamada com mais de 7 dias não deve permitir edição");
    }
}
