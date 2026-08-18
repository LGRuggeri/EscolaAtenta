using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Usuarios.Queries;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetUsuariosQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetUsuariosQueryHandlerTests()
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

    private static GetUsuariosQueryHandler CriarHandler(AppDbContext ctx) => new(ctx);

    private static Usuario CriarUsuario(string nome, string email, PapelUsuario papel = PapelUsuario.Monitor) =>
        new(nome, email, "$2a$11$fakehash000000000000000000000000000000000000000000000000", papel);

    [Fact]
    public async Task Handle_SemUsuarios_DeveRetornarPaginaVazia()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery(), CancellationToken.None);

        resultado.Items.Should().BeEmpty();
        resultado.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ComUsuarios_DeveRetornarOrdenadoPorNome()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuario("Zélia", "zelia@escola.com"));
        ctx.Usuarios.Add(CriarUsuario("Ana", "ana@escola.com"));
        ctx.Usuarios.Add(CriarUsuario("Carlos", "carlos@escola.com"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery(), CancellationToken.None);

        resultado.Items.Should().HaveCount(3);
        resultado.Items[0].Nome.Should().Be("Ana");
        resultado.Items[1].Nome.Should().Be("Carlos");
        resultado.Items[2].Nome.Should().Be("Zélia");
    }

    [Fact]
    public async Task Handle_SearchTerm_DeveFiltraPorNomeOuEmail()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuario("João Silva", "joao@escola.com"));
        ctx.Usuarios.Add(CriarUsuario("Maria Santos", "maria@escola.com"));
        ctx.Usuarios.Add(CriarUsuario("Pedro Admin", "admin@escola.com"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Busca por nome
        var resultadoNome = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery { SearchTerm = "João" }, CancellationToken.None);
        resultadoNome.TotalCount.Should().Be(1);
        resultadoNome.Items.First().Nome.Should().Be("João Silva");

        // Busca por email
        var resultadoEmail = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery { SearchTerm = "admin@" }, CancellationToken.None);
        resultadoEmail.TotalCount.Should().Be(1);
        resultadoEmail.Items.First().Nome.Should().Be("Pedro Admin");
    }

    [Fact]
    public async Task Handle_FiltroPapel_DeveRetornarApenasPapelSolicitado()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuario("Monitor 1", "mon1@escola.com", PapelUsuario.Monitor));
        ctx.Usuarios.Add(CriarUsuario("Admin 1", "adm1@escola.com", PapelUsuario.Administrador));
        ctx.Usuarios.Add(CriarUsuario("Monitor 2", "mon2@escola.com", PapelUsuario.Monitor));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery { Papel = PapelUsuario.Monitor }, CancellationToken.None);

        resultado.TotalCount.Should().Be(2);
        resultado.Items.Should().AllSatisfy(u => u.Papel.Should().Be("Monitor"));
    }

    [Fact]
    public async Task Handle_Paginacao_DeveRespeitarPageSize()
    {
        await using var ctx = CriarContexto();
        for (int i = 0; i < 5; i++)
            ctx.Usuarios.Add(CriarUsuario($"User {i}", $"user{i}@escola.com"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery(PageNumber: 1, PageSize: 2), CancellationToken.None);

        resultado.Items.Should().HaveCount(2);
        resultado.TotalCount.Should().Be(5);
        resultado.HasNextPage.Should().BeTrue();
        resultado.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_BoundsGuard_PageSizeClampado()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuario("Teste", "teste@escola.com"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery(PageNumber: 1, PageSize: 0), CancellationToken.None);

        resultado.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DtoDeveTerCamposPreenchidos()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuario("Roberto Souza", "roberto@escola.com", PapelUsuario.Administrador));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuariosQuery(), CancellationToken.None);

        var dto = resultado.Items.Single();
        dto.Id.Should().NotBeEmpty();
        dto.Nome.Should().Be("Roberto Souza");
        dto.Email.Should().Be("roberto@escola.com");
        dto.Papel.Should().Be("Administrador");
        dto.Ativo.Should().BeTrue();
    }
}
