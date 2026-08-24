using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
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

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var dataChamada = new DateTime(2025, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(dataChamada), turma.Id, usuario.Id);
        chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
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

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
            CancellationToken.None);

        resultado.Resumo.TotalAlunos.Should().Be(0);
        resultado.Resumo.TotalPresentes.Should().Be(0);
    }

    [Fact]
    public async Task Handle_QuandoAlunoEstaInativo_DeveIncluirNoRelatorioHistorico()
    {
        await using var ctx = CriarContexto();

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        aluno.Desativar("sistema");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, 10, 8, 0, 0, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
            CancellationToken.None);

        resultado.Alunos.Should().ContainSingle(a => a.AlunoId == aluno.Id);
        resultado.Resumo.TotalAlunos.Should().Be(1);
        resultado.Resumo.TotalPresentes.Should().Be(1);
        resultado.Resumo.PercentualPresencaTurma.Should().Be(100);
    }

    [Fact]
    public async Task Handle_ChamadaNoUltimoDiaComSubsegundos_DeveSerIncluida()
    {
        await using var ctx = CriarContexto();

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, 30, 23, 59, 59, 500, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
            CancellationToken.None);

        resultado.Resumo.TotalPresentes.Should().Be(1);
        resultado.Resumo.TotalAlunos.Should().Be(1);
        resultado.PeriodoFim.Should().Be(new DateTime(2025, 4, 30));
    }

    [Fact]
    public async Task Handle_QuandoDataInicioMaiorQueDataFim_DeveLancarDomainException()
    {
        await using var ctx = CriarContexto();

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var acao = async () => await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 30), new DateTime(2025, 4, 1)),
            CancellationToken.None);

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_QuandoAlunoTemFaltas_DeveRetornarRelatorioComFaltas()
    {
        await using var ctx = CriarContexto();

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var datas = new[] { 10, 11, 12 };
        foreach (var dia in datas)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, dia, 8, 0, 0, TimeSpan.Zero), turma.Id, usuario.Id);
            chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);
            ctx.Chamadas.Add(chamada);
        }

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
            CancellationToken.None);

        resultado.TurmaId.Should().Be(turma.Id);
        resultado.Alunos.Should().ContainSingle(a => a.AlunoId == aluno.Id);

        var alunoDto = resultado.Alunos.Single(a => a.AlunoId == aluno.Id);
        alunoDto.Faltas.Should().Be(3);
        alunoDto.Presentes.Should().Be(0);
        alunoDto.PercentualPresenca.Should().Be(0);

        resultado.Resumo.TotalFaltas.Should().Be(3);
        resultado.Resumo.TotalPresentes.Should().Be(0);
        resultado.Resumo.PercentualPresencaTurma.Should().Be(0);
    }

    [Fact]
    public async Task Handle_QuandoPresencaQuebraSequenciaDeFaltas_DeveZerarContadorRelatorio()
    {
        await using var ctx = CriarContexto();

        var turma = new Turma(Guid.NewGuid(), "5º Ano A", "Manhã", 2025);
        ctx.Turmas.Add(turma);

        var usuario = new Usuario("Monitor", "monitor@teste.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);

        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT001", turma.Id);
        aluno.Matricular(turma.Id, 2025, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Matrícula inicial");
        ctx.Alunos.Add(aluno);

        await ctx.SaveChangesAsync();

        var chamada1 = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, 10, 8, 0, 0, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada1.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);
        ctx.Chamadas.Add(chamada1);

        var chamada2 = new Chamada(Guid.NewGuid(), new DateTimeOffset(2025, 4, 11, 8, 0, 0, TimeSpan.Zero), turma.Id, usuario.Id);
        chamada2.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        ctx.Chamadas.Add(chamada2);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new RelatorioTurmaHandler(ctx, NullLogger<RelatorioTurmaHandler>.Instance);
        var resultado = await handler.Handle(
            new RelatorioTurmaQuery(turma.Id, new DateTime(2025, 4, 1), new DateTime(2025, 4, 30)),
            CancellationToken.None);

        var alunoDto = resultado.Alunos.Single(a => a.AlunoId == aluno.Id);
        alunoDto.Faltas.Should().Be(1);
        alunoDto.Presentes.Should().Be(1);
        alunoDto.PercentualPresenca.Should().Be(50);
    }

    // ── Compatibilidade OTA: contrato legado anoLetivo/periodoLetivo ────────────

    [Fact]
    public void DePeriodoLetivo_AnoSemPeriodo_DeveUsarPeriodoAtualDoAnoCorrente()
    {
        var turmaId = Guid.NewGuid();
        var hoje = DateTime.Today;

        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            turmaId, hoje.Year, TipoPeriodoLetivo.Trimestre);

        query.TurmaId.Should().Be(turmaId);
        query.DataInicio.Should().BeOnOrBefore(hoje);
        query.DataFim.Should().BeOnOrAfter(hoje);
        query.DataInicio.Year.Should().Be(hoje.Year);
    }

    [Fact]
    public void DePeriodoLetivo_AnoSemPeriodoAnoDiferente_DeveUsarPrimeiroPeriodo()
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), DateTime.Today.Year - 1, TipoPeriodoLetivo.Trimestre);

        query.DataInicio.Should().Be(new DateTime(DateTime.Today.Year - 1, 1, 1));
        query.DataFim.Should().Be(new DateTime(DateTime.Today.Year - 1, 3, 31));
    }

    [Theory]
    [InlineData(1, 1, 1, 3, 31)]
    [InlineData(2, 4, 1, 6, 30)]
    [InlineData(3, 7, 1, 9, 30)]
    [InlineData(4, 10, 1, 12, 31)]
    public void DePeriodoLetivo_Trimestre_DeveRetornarIntervaloCorreto(
        int periodo, int mesInicio, int diaInicio, int mesFim, int diaFim)
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2025, TipoPeriodoLetivo.Trimestre, periodo);

        query.DataInicio.Should().Be(new DateTime(2025, mesInicio, diaInicio));
        query.DataFim.Should().Be(new DateTime(2025, mesFim, diaFim));
    }

    [Theory]
    [InlineData(1, 1, 1, 6, 30)]
    [InlineData(2, 7, 1, 12, 31)]
    public void DePeriodoLetivo_Semestre_DeveRetornarIntervaloCorreto(
        int periodo, int mesInicio, int diaInicio, int mesFim, int diaFim)
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2025, TipoPeriodoLetivo.Semestre, periodo);

        query.DataInicio.Should().Be(new DateTime(2025, mesInicio, diaInicio));
        query.DataFim.Should().Be(new DateTime(2025, mesFim, diaFim));
    }

    [Theory]
    [InlineData(1, 1, 1, 2, 28)]
    [InlineData(2, 3, 1, 4, 30)]
    [InlineData(3, 5, 1, 6, 30)]
    [InlineData(4, 7, 1, 8, 31)]
    [InlineData(5, 9, 1, 12, 31)]
    [InlineData(6, 9, 1, 12, 31)]   // período maior que 5 clamp para 5º bimestre legado (set-dez)
    public void DePeriodoLetivo_Bimestre_DeveRetornarIntervaloCorreto(
        int periodo, int mesInicio, int diaInicio, int mesFim, int diaFim)
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2025, TipoPeriodoLetivo.Bimestre, periodo);

        query.DataInicio.Should().Be(new DateTime(2025, mesInicio, diaInicio));
        query.DataFim.Should().Be(new DateTime(2025, mesFim, diaFim));
    }

    [Fact]
    public void DePeriodoLetivo_PrimeiroBimestreAnoBissexto_DeveTerminarEm29DeFevereiro()
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2024, TipoPeriodoLetivo.Bimestre, 1);

        query.DataInicio.Should().Be(new DateTime(2024, 1, 1));
        query.DataFim.Should().Be(new DateTime(2024, 2, 29));
    }

    [Fact]
    public void DePeriodoLetivo_PeriodoMaiorQueQuatroEmTrimestre_DeveClamparParaQuartoTrimestre()
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2025, TipoPeriodoLetivo.Trimestre, 99);

        query.DataInicio.Should().Be(new DateTime(2025, 10, 1));
        query.DataFim.Should().Be(new DateTime(2025, 12, 31));
    }

    [Fact]
    public void DePeriodoLetivo_PeriodoZeroEmTrimestre_DeveClamparParaPrimeiroTrimestre()
    {
        var query = RelatorioTurmaQuery.DePeriodoLetivo(
            Guid.NewGuid(), 2025, TipoPeriodoLetivo.Trimestre, 0);

        query.DataInicio.Should().Be(new DateTime(2025, 1, 1));
        query.DataFim.Should().Be(new DateTime(2025, 3, 31));
    }
}
