using EscolaAtenta.Application.Alertas.Handlers;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

/// <summary>
/// Usa InMemoryDatabase pois o handler faz OrderByDescending(DateTimeOffset)
/// que o provider SQLite não suporta.
/// </summary>
public class GetAuditoriaAlertasQueryHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static GetAuditoriaAlertasQueryHandler CriarHandler(AppDbContext ctx) => new(ctx);

    private static async Task SeedAlertaResolvido(
        AppDbContext ctx,
        TipoAlerta tipo = TipoAlerta.Evasao,
        NivelAlertaFalta nivel = NivelAlertaFalta.Aviso,
        string nomeAluno = "Aluno Teste")
    {
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var resolvedorId = Guid.NewGuid();

        ctx.Turmas.Add(new Turma(turmaId, "Turma Auditoria", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, nomeAluno, null, turmaId));
        ctx.Usuarios.Add(new Usuario("Resolvedor", $"resolvedor{Guid.NewGuid():N}@escola.com", "$2a$11$fakehash000000000000000000000000000000000000000000000000", PapelUsuario.Administrador));

        var alerta = tipo == TipoAlerta.Atraso
            ? AlertaEvasao.CriarAlertaAtraso(alunoId, turmaId, nivel, "Atrasos excessivos")
            : AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, nivel, "Faltas excessivas");

        alerta.MarcarComoResolvido(resolvedorId, "Justificativa de teste");
        ctx.AlertasEvasao.Add(alerta);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_SemAlertasResolvidos_DeveRetornarPaginaVazia()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAuditoriaAlertasQuery(), CancellationToken.None);

        resultado.Items.Should().BeEmpty();
        resultado.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ApenasAlertasResolvidos_NaoDeveRetornarNaoResolvidos()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno", null, turmaId));

        // Alerta NÃO resolvido — não deve aparecer
        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Aberto"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAuditoriaAlertasQuery(), CancellationToken.None);

        resultado.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ComAlertasResolvidos_DeveRetornarItens()
    {
        await using var ctx = CriarContexto();
        await SeedAlertaResolvido(ctx);

        var resultado = await CriarHandler(ctx).Handle(
            new GetAuditoriaAlertasQuery(), CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_FiltroNomeAluno_DeveRetornarApenasFiltrados()
    {
        await using var ctx = CriarContexto();
        await SeedAlertaResolvido(ctx, nomeAluno: "João Silva");
        await SeedAlertaResolvido(ctx, nomeAluno: "Maria Santos");

        var query = new GetAuditoriaAlertasQuery { NomeAluno = "João" };
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.First().NomeAluno.Should().Be("João Silva");
    }

    [Fact]
    public async Task Handle_FiltroTipo_DeveRetornarApenasTipoSolicitado()
    {
        await using var ctx = CriarContexto();
        await SeedAlertaResolvido(ctx, tipo: TipoAlerta.Evasao);
        await SeedAlertaResolvido(ctx, tipo: TipoAlerta.Atraso);

        var query = new GetAuditoriaAlertasQuery { Tipo = TipoAlerta.Atraso };
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.First().TipoAlerta.Should().Be("Atraso");
    }

    [Fact]
    public async Task Handle_Paginacao_DeveRespeitarPageSize()
    {
        await using var ctx = CriarContexto();
        for (int i = 0; i < 5; i++)
            await SeedAlertaResolvido(ctx, nomeAluno: $"Aluno {i}");

        var query = new GetAuditoriaAlertasQuery(PageNumber: 1, PageSize: 2);
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.Items.Should().HaveCount(2);
        resultado.TotalCount.Should().Be(5);
        resultado.HasNextPage.Should().BeTrue();
        resultado.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_BoundsGuard_PageSizeClampado()
    {
        await using var ctx = CriarContexto();
        await SeedAlertaResolvido(ctx);

        // PageSize 0 clampado para 1
        var query = new GetAuditoriaAlertasQuery(PageNumber: 1, PageSize: 0);
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DtoDeveTerCamposPreenchidos()
    {
        await using var ctx = CriarContexto();
        await SeedAlertaResolvido(ctx, nomeAluno: "Roberto Souza", nivel: NivelAlertaFalta.Vermelho);

        var resultado = await CriarHandler(ctx).Handle(
            new GetAuditoriaAlertasQuery(), CancellationToken.None);

        var dto = resultado.Items.First();
        dto.Id.Should().NotBeEmpty();
        dto.NomeAluno.Should().Be("Roberto Souza");
        dto.TipoAlerta.Should().Be("Evasao");
        dto.NivelAlerta.Should().Be("Vermelho");
        dto.MotivoResolucao.Should().Be("Justificativa de teste");
        dto.DataResolucao.Should().NotBe(DateTimeOffset.MinValue);
    }
}
