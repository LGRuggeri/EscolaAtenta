using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class AtualizarAlunoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoAlunoNaoEncontrado_DeveDispararKeyNotFoundException()
    {
        await using var ctx = CriarContexto();
        var handler = new AtualizarAlunoHandler(ctx, new FakeCurrentUserService(), NullLogger<AtualizarAlunoHandler>.Instance);

        Func<Task> act = () => handler.Handle(
            new AtualizarAlunoCommand(Guid.NewGuid(), "Nome", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_QuandoAlunoExiste_DeveAtualizarNomeEMatricula()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var aluno = new Aluno(Guid.NewGuid(), "Nome Original", "MAT001", turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        var handler = new AtualizarAlunoHandler(ctx, new FakeCurrentUserService(), NullLogger<AtualizarAlunoHandler>.Instance);
        await handler.Handle(new AtualizarAlunoCommand(aluno.Id, "Nome Atualizado", "MAT002"), CancellationToken.None);

        var salvo = await ctx.Alunos.FindAsync(aluno.Id);
        salvo!.Nome.Should().Be("Nome Atualizado");
        salvo.Matricula.Should().Be("MAT002");
    }
}
