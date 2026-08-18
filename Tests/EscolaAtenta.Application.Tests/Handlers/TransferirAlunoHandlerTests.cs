using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Application.Alunos.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class TransferirAlunoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoAlunoETurmaExistem_DeveTransferirECriarHistorico()
    {
        await using var ctx = CriarContexto();

        var turmaOrigem = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        var turmaDestino = new Turma(Guid.NewGuid(), "6º Ano B", "Manhã", 2026);
        ctx.Turmas.AddRange(turmaOrigem, turmaDestino);

        var aluno = new Aluno(Guid.NewGuid(), "João", "MAT001", turmaOrigem.Id);
        aluno.Matricular(turmaOrigem.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        var handler = new TransferirAlunoHandler(ctx, new FakeCurrentUserService(), NullLogger<TransferirAlunoHandler>.Instance);

        await handler.Handle(new TransferirAlunoCommand(
            aluno.Id,
            turmaDestino.Id,
            new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc),
            "Promoção"), CancellationToken.None);

        var alunoAtualizado = await ctx.Alunos
            .AsNoTracking()
            .Include(a => a.HistoricoTurmas)
            .FirstAsync(a => a.Id == aluno.Id);

        alunoAtualizado.TurmaId.Should().Be(turmaDestino.Id);
        alunoAtualizado.HistoricoTurmas.Should().HaveCount(2);
        alunoAtualizado.ObterMatriculaAtiva()!.TurmaId.Should().Be(turmaDestino.Id);
    }

    [Fact]
    public async Task Handle_QuandoAlunoNaoTemHistorico_DeveRetroalimentarETransferir()
    {
        await using var ctx = CriarContexto();

        var turmaOrigem = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        var turmaDestino = new Turma(Guid.NewGuid(), "6º Ano B", "Manhã", 2026);
        ctx.Turmas.AddRange(turmaOrigem, turmaDestino);

        var aluno = new Aluno(Guid.NewGuid(), "Maria", "MAT002", turmaOrigem.Id);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        var handler = new TransferirAlunoHandler(ctx, new FakeCurrentUserService(), NullLogger<TransferirAlunoHandler>.Instance);

        await handler.Handle(new TransferirAlunoCommand(
            aluno.Id,
            turmaDestino.Id,
            new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc),
            "Promoção"), CancellationToken.None);

        var alunoAtualizado = await ctx.Alunos
            .AsNoTracking()
            .Include(a => a.HistoricoTurmas)
            .FirstAsync(a => a.Id == aluno.Id);

        alunoAtualizado.HistoricoTurmas.Should().HaveCount(2);
    }
}
