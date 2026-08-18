using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class ObterHistoricoTurmasAlunoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoAlunoTemHistorico_DeveRetornarHistoricoOrdenado()
    {
        await using var ctx = CriarContexto();

        var turma1 = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        var turma2 = new Turma(Guid.NewGuid(), "6º Ano A", "Manhã", 2026);
        ctx.Turmas.AddRange(turma1, turma2);

        var aluno = new Aluno(Guid.NewGuid(), "Ana", "MAT001", turma1.Id);
        aluno.Matricular(turma1.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var novaMatricula = aluno.TransferirTurma(turma2.Id, 2026, new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc), "Promoção");
        ctx.AlunosTurmasHistorico.Add(novaMatricula);
        await ctx.SaveChangesAsync();

        var handler = new ObterHistoricoTurmasAlunoHandler(ctx, NullLogger<ObterHistoricoTurmasAlunoHandler>.Instance);
        var resultado = (await handler.Handle(new ObterHistoricoTurmasAlunoQuery(aluno.Id.ToString()), CancellationToken.None)).ToList();

        resultado.Should().HaveCount(2);
        resultado.First().TurmaId.Should().Be(turma2.Id);
        resultado.First().Ativa.Should().BeTrue();
        resultado.Last().Ativa.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_QuandoAlunoNaoExiste_DeveRetornarVazio()
    {
        await using var ctx = CriarContexto();

        var handler = new ObterHistoricoTurmasAlunoHandler(ctx, NullLogger<ObterHistoricoTurmasAlunoHandler>.Instance);
        var resultado = await handler.Handle(new ObterHistoricoTurmasAlunoQuery(Guid.NewGuid().ToString()), CancellationToken.None);

        resultado.Should().BeEmpty();
    }
}
