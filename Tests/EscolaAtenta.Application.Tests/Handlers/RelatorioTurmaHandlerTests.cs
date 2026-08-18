using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class RelatorioTurmaHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoTurmaTemAlunosERegistros_DeveRetornarRelatorioCorreto()
    {
        await using var ctx = CriarContexto();

        var configuracao = new ConfiguracaoEscola(Guid.NewGuid(), TipoPeriodoLetivo.Trimestre);
        ctx.ConfiguracoesEscola.Add(configuracao);

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, 10, 8, 0, 0, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, 2025, 2), // 2º trimestre: 01/04 a 30/06
            CancellationToken.None);

        resultado.TurmaId.Should().Be(turma.Id);
        resultado.Alunos.Should().ContainSingle(a => a.AlunoId == aluno.Id);
        resultado.Resumo.TotalAlunos.Should().Be(1);
        resultado.Resumo.TotalPresentes.Should().Be(1);
        resultado.Resumo.PercentualPresencaTurma.Should().Be(100);
    }

    [Fact]
    public async Task Handle_QuandoNaoHaAlunosMatriculados_DeveRetornarRelatorioVazio()
    {
        await using var ctx = CriarContexto();

        var configuracao = new ConfiguracaoEscola(Guid.NewGuid(), TipoPeriodoLetivo.Trimestre);
        ctx.ConfiguracoesEscola.Add(configuracao);

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, 2025, 1),
            CancellationToken.None);

        resultado.Resumo.TotalAlunos.Should().Be(0);
        resultado.Resumo.TotalPresentes.Should().Be(0);
    }

    [Fact]
    public async Task Handle_QuandoPeriodoNaoInformado_DeveUsarPeriodoAtual()
    {
        await using var ctx = CriarContexto();

        var anoLetivo = DateTime.UtcNow.Year;
        var configuracao = new ConfiguracaoEscola(Guid.NewGuid(), TipoPeriodoLetivo.Trimestre);
        ctx.ConfiguracoesEscola.Add(configuracao);

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", anoLetivo);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, anoLetivo, new DateTime(anoLetivo, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var periodoAtual = CalendarioEscolar.ObterPeriodoAtual(DateTime.UtcNow, TipoPeriodoLetivo.Trimestre, anoLetivo);
        var (inicio, _) = CalendarioEscolar.ObterPeriodo(anoLetivo, TipoPeriodoLetivo.Trimestre, periodoAtual);
        var dataChamada = inicio.AddDays(5);

        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(dataChamada, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, anoLetivo, null),
            CancellationToken.None);

        resultado.TurmaId.Should().Be(turma.Id);
        resultado.Alunos.Should().ContainSingle(a => a.AlunoId == aluno.Id);
        resultado.Resumo.TotalPresentes.Should().Be(1);
        resultado.Resumo.PercentualPresencaTurma.Should().Be(100);
    }
}
