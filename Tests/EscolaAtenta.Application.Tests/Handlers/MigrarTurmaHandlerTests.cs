using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class MigrarTurmaHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoTurmasExistem_DeveMigrarTodosOsAlunos()
    {
        await using var ctx = CriarContexto();

        var origem = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        var destino = new Turma(Guid.NewGuid(), "6º Ano A", "Manhã", 2026);
        ctx.Turmas.AddRange(origem, destino);

        for (int i = 0; i < 3; i++)
        {
            var aluno = new Aluno(Guid.NewGuid(), $"Aluno {i}", $"MAT{i:000}", origem.Id);
            aluno.Matricular(origem.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
            ctx.Alunos.Add(aluno);
        }

        await ctx.SaveChangesAsync();

        var handler = new MigrarTurmaHandler(ctx, new FakeCurrentUserService(), NullLogger<MigrarTurmaHandler>.Instance);
        var resultado = await handler.Handle(new MigrarTurmaCommand(
            origem.Id,
            destino.Id,
            new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc),
            "Promoção de série"), CancellationToken.None);

        resultado.QuantidadeTransferida.Should().Be(3);
        resultado.QuantidadeIgnorada.Should().Be(0);

        var alunosDestino = await ctx.Alunos
            .AsNoTracking()
            .Where(a => a.TurmaId == destino.Id)
            .CountAsync();

        alunosDestino.Should().Be(3);
    }

    [Fact]
    public async Task Handle_QuandoUsuarioNaoEhAdministrador_DeveLancarDomainException()
    {
        await using var ctx = CriarContexto();

        var origem = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        var destino = new Turma(Guid.NewGuid(), "6º Ano A", "Manhã", 2026);
        ctx.Turmas.AddRange(origem, destino);
        await ctx.SaveChangesAsync();

        var handler = new MigrarTurmaHandler(
            ctx,
            new FakeCurrentUserService { Papel = "Monitor" },
            NullLogger<MigrarTurmaHandler>.Instance);

        Func<Task> act = () => handler.Handle(new MigrarTurmaCommand(
            origem.Id,
            destino.Id,
            new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc),
            "Promoção de série"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
