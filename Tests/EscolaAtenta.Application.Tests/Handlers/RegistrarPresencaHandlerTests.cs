using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class RegistrarPresencaHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RegistrarPresencaHandlerTests()
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
        // Desativa FK constraints para testes: entidades relacionadas (Turma, Usuário)
        // não precisam existir no banco para testar o fluxo de presença.
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private RegistrarPresencaHandler CriarHandler(AppDbContext ctx) =>
        new(ctx, null!, NullLogger<RegistrarPresencaHandler>.Instance);

    [Fact]
    public async Task Handle_QuandoChamadaNaoEncontrada_DeveDispararDomainException()
    {
        await using var ctx = CriarContexto();
        Func<Task> act = () => CriarHandler(ctx).Handle(
            new RegistrarPresencaCommand(Guid.NewGuid(), Guid.NewGuid(), StatusPresenca.Presente),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Chamada*");
    }

    [Fact]
    public async Task Handle_QuandoAlunoNaoEncontrado_DeveDispararDomainException()
    {
        await using var ctx = CriarContexto();
        var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        ctx.Chamadas.Add(chamada);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();

        Func<Task> act = () => CriarHandler(ctx).Handle(
            new RegistrarPresencaCommand(chamada.Id, Guid.NewGuid(), StatusPresenca.Presente),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Aluno*");
    }

    [Fact]
    public async Task Handle_RegistrarPresenca_DeveRetornarRegistroIdEAlertaNaoGerado()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, turmaId, Guid.NewGuid());
        var aluno = new Aluno(Guid.NewGuid(), "João", null, turmaId);
        ctx.Chamadas.Add(chamada);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new RegistrarPresencaCommand(chamada.Id, aluno.Id, StatusPresenca.Presente),
            CancellationToken.None);

        resultado.RegistroPresencaId.Should().NotBeEmpty();
        resultado.AlertaGerado.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RegistrarFalta_DeveIncrementarContadoresDoAluno()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, turmaId, Guid.NewGuid());
        var aluno = new Aluno(Guid.NewGuid(), "Maria", null, turmaId);
        ctx.Chamadas.Add(chamada);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();

        await CriarHandler(ctx).Handle(
            new RegistrarPresencaCommand(chamada.Id, aluno.Id, StatusPresenca.Falta),
            CancellationToken.None);

        ctx.ChangeTracker.Clear();

        var salvo = await ctx.Alunos.IgnoreQueryFilters().FirstAsync(a => a.Id == aluno.Id);
        salvo.TotalFaltas.Should().Be(1);
        salvo.FaltasConsecutivasAtuais.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AoAtingirLimiteDeFaltas_DeveIndicarAlertaGerado()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var aluno = new Aluno(Guid.NewGuid(), "Ana", null, turmaId);
        ctx.Alunos.Add(aluno);

        // Seed 3 chamadas
        var chamadas = Enumerable.Range(0, 3)
            .Select(i => new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-i), turmaId, Guid.NewGuid()))
            .ToList();
        ctx.Chamadas.AddRange(chamadas);
        await ctx.SaveChangesAsync();

        // Registra 2 faltas (não gera alerta ainda)
        foreach (var c in chamadas.Take(2))
        {
            ctx.ChangeTracker.Clear();
            await CriarHandler(ctx).Handle(
                new RegistrarPresencaCommand(c.Id, aluno.Id, StatusPresenca.Falta),
                CancellationToken.None);
        }

        // 3ª falta: deve gerar alerta (FaltasConsecutivasAtuais == 3)
        ctx.ChangeTracker.Clear();
        var resultado = await CriarHandler(ctx).Handle(
            new RegistrarPresencaCommand(chamadas[2].Id, aluno.Id, StatusPresenca.Falta),
            CancellationToken.None);

        resultado.AlertaGerado.Should().BeTrue();
    }
}
