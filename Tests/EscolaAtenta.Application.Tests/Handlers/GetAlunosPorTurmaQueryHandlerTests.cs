using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetAlunosPorTurmaQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetAlunosPorTurmaQueryHandlerTests()
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

    private static GetAlunosPorTurmaQueryHandler CriarHandler(AppDbContext ctx, FakeCurrentUserService? currentUser = null) =>
        new(ctx, currentUser ?? new FakeCurrentUserService(), NullLogger<GetAlunosPorTurmaQueryHandler>.Instance);

    [Fact]
    public async Task Handle_TurmaInexistente_DeveLancarKeyNotFoundException()
    {
        await using var ctx = CriarContexto();

        Func<Task> act = () => CriarHandler(ctx).Handle(
            new GetAlunosPorTurmaQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_TurmaVazia_DeveRetornarListaVazia()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "1º Ano", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosPorTurmaQuery(turmaId), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TurmaComAlunos_DeveRetornarAlunosOrdenadosPorNome()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "2º Ano", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Zélia", "MAT003", turmaId));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Ana", "MAT001", turmaId));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Carlos", "MAT002", turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosPorTurmaQuery(turmaId), CancellationToken.None);

        resultado.Should().HaveCount(3);
        resultado[0].Nome.Should().Be("Ana");
        resultado[1].Nome.Should().Be("Carlos");
        resultado[2].Nome.Should().Be("Zélia");
    }

    [Fact]
    public async Task Handle_DeveMapearDtoCorretamente()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "3º Ano", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "João Silva", "MAT-100", turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlunosPorTurmaQuery(turmaId), CancellationToken.None);

        var dto = resultado.Single();
        dto.Id.Should().Be(alunoId);
        dto.Nome.Should().Be("João Silva");
        dto.Matricula.Should().Be("MAT-100");
        dto.TurmaId.Should().Be(turmaId);
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
        ctx.Turmas.Add(new Turma(turmaId, "4º Ano", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Teste", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        Func<Task> act = () => CriarHandler(ctx, fakeUser).Handle(
            new GetAlunosPorTurmaQuery(turmaId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_MonitorComVinculo_DeveRetornarAlunos()
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
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(Guid.NewGuid(), "Aluno", null, turmaId));
        ctx.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), monitorId, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx, fakeUser).Handle(
            new GetAlunosPorTurmaQuery(turmaId), CancellationToken.None);

        resultado.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NaoRetornaAlunosDeOutraTurma()
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
            new GetAlunosPorTurmaQuery(turmaA), CancellationToken.None);

        resultado.Should().HaveCount(1);
        resultado[0].Nome.Should().Be("Aluno A");
    }
}
