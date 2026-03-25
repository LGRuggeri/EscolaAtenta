using EscolaAtenta.Application.Alertas.Handlers;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetAlertasHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetAlertasHandlerTests()
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
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private static GetAlertasHandler CriarHandler(AppDbContext ctx) => new(ctx);

    private async Task SeedAlertas(AppDbContext ctx, int quantidade, bool resolvidos = false)
    {
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Alerta", "Manhã", 2026));

        for (int i = 0; i < quantidade; i++)
        {
            var alunoId = Guid.NewGuid();
            ctx.Alunos.Add(new Aluno(alunoId, $"Aluno {i}", null, turmaId));

            var alerta = AlertaEvasao.CriarAlertaAluno(
                alunoId, turmaId, NivelAlertaFalta.Aviso, $"Motivo {i}");

            if (resolvidos)
            {
                alerta.MarcarComoResolvido(Guid.NewGuid(), $"Resolvido {i}");
            }

            ctx.AlertasEvasao.Add(alerta);
        }

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_SemAlertas_DeveRetornarPaginaVazia()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true),
            CancellationToken.None);

        resultado.Items.Should().BeEmpty();
        resultado.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ApenasNaoResolvidos_DeveOmitirResolvidos()
    {
        await using var ctx = CriarContexto();
        await SeedAlertas(ctx, 3, resolvidos: false);
        await SeedAlertas(ctx, 2, resolvidos: true);

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true),
            CancellationToken.None);

        resultado.TotalCount.Should().Be(3);
        resultado.Items.Should().AllSatisfy(a => a.Resolvido.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_TodosAlertas_DeveIncluirResolvidos()
    {
        await using var ctx = CriarContexto();
        await SeedAlertas(ctx, 2, resolvidos: false);
        await SeedAlertas(ctx, 3, resolvidos: true);

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: false),
            CancellationToken.None);

        resultado.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Paginacao_DeveRespeitarPageSizeEPageNumber()
    {
        await using var ctx = CriarContexto();
        await SeedAlertas(ctx, 10, resolvidos: false);

        var pagina1 = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true, PageNumber: 1, PageSize: 3),
            CancellationToken.None);

        pagina1.Items.Should().HaveCount(3);
        pagina1.TotalCount.Should().Be(10);
        pagina1.PageNumber.Should().Be(1);
        pagina1.PageSize.Should().Be(3);
        pagina1.HasNextPage.Should().BeTrue();
        pagina1.HasPreviousPage.Should().BeFalse();
        pagina1.TotalPages.Should().Be(4); // ceil(10/3)
    }

    [Fact]
    public async Task Handle_PaginacaoUltimaPagina_DeveRetornarRestante()
    {
        await using var ctx = CriarContexto();
        await SeedAlertas(ctx, 10, resolvidos: false);

        var ultimaPagina = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true, PageNumber: 4, PageSize: 3),
            CancellationToken.None);

        ultimaPagina.Items.Should().HaveCount(1); // 10 - 9 = 1
        ultimaPagina.HasNextPage.Should().BeFalse();
        ultimaPagina.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_BoundsGuard_PageSizeClampado()
    {
        await using var ctx = CriarContexto();
        await SeedAlertas(ctx, 5, resolvidos: false);

        // PageSize 0 deve ser clampado para 1
        var resultado = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true, PageNumber: 1, PageSize: 0),
            CancellationToken.None);

        resultado.Items.Should().HaveCount(1);
        resultado.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FiltroTipoEvasao_DeveRetornarApenasFaltas()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Filtro", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Filtro", null, turmaId));

        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Evasão"));
        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAtraso(alunoId, turmaId, NivelAlertaFalta.Aviso, "Atraso"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var query = new GetAlertasQuery(ApenasNaoResolvidos: true) { Tipo = TipoAlerta.Evasao };
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.Should().AllSatisfy(a => a.Tipo.Should().Be("Evasao"));
    }

    [Fact]
    public async Task Handle_FiltroTipoAtraso_DeveRetornarApenasAtrasos()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Filtro2", "Tarde", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Filtro2", null, turmaId));

        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Evasão"));
        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAtraso(alunoId, turmaId, NivelAlertaFalta.Aviso, "Atraso"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var query = new GetAlertasQuery(ApenasNaoResolvidos: true) { Tipo = TipoAlerta.Atraso };
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.Should().AllSatisfy(a => a.Tipo.Should().Be("Atraso"));
    }

    [Fact]
    public async Task Handle_FiltroNivelSoFuncaonaComEvasao()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Nivel", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Nivel", null, turmaId));

        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Leve"));
        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Vermelho, "Grave"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Filtro por Nivel só funciona com Tipo=Evasao
        var query = new GetAlertasQuery(ApenasNaoResolvidos: true)
        {
            Tipo = TipoAlerta.Evasao,
            Nivel = NivelAlertaFalta.Vermelho
        };
        var resultado = await CriarHandler(ctx).Handle(query, CancellationToken.None);

        resultado.TotalCount.Should().Be(1);
        resultado.Items.First().Nivel.Should().Be(NivelAlertaFalta.Vermelho);
    }

    [Fact]
    public async Task Handle_DtoDeveTerCamposPreenchidos()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano B", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "João Silva", "MAT-001", turmaId));
        ctx.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Intermediario, "2 faltas"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetAlertasQuery(ApenasNaoResolvidos: true),
            CancellationToken.None);

        var dto = resultado.Items.First();
        dto.NomeAluno.Should().Be("João Silva");
        dto.NomeTurma.Should().Be("5º Ano B");
        dto.Nivel.Should().Be(NivelAlertaFalta.Intermediario);
        dto.TituloAmigavel.Should().NotBeNullOrEmpty();
        dto.MensagemAcao.Should().Contain("João Silva");
        dto.Tipo.Should().Be("Evasao");
        dto.Resolvido.Should().BeFalse();
    }
}
