using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class CriarAlunoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoTurmaNaoExiste_DeveDispararArgumentException()
    {
        await using var ctx = CriarContexto();
        var handler = new CriarAlunoHandler(ctx);

        Func<Task> act = () => handler.Handle(
            new CriarAlunoCommand("Ana", null, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Turma*");
    }

    [Fact]
    public async Task Handle_QuandoTurmaExiste_DeveCriarAlunoERetornarDto()
    {
        await using var ctx = CriarContexto();
        var turma = new Turma(Guid.NewGuid(), "1º Ano A", "Manhã", 2026);
        ctx.Turmas.Add(turma);
        await ctx.SaveChangesAsync();

        var handler = new CriarAlunoHandler(ctx);
        var resultado = await handler.Handle(
            new CriarAlunoCommand("Carlos", "MAT099", turma.Id),
            CancellationToken.None);

        resultado.Id.Should().NotBeEmpty();
        resultado.Nome.Should().Be("Carlos");
        resultado.Matricula.Should().Be("MAT099");
        resultado.TurmaId.Should().Be(turma.Id);
    }
}
