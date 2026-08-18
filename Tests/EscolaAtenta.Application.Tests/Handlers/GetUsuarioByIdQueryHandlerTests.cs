using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Usuarios.Queries;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetUsuarioByIdQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetUsuarioByIdQueryHandlerTests()
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

    private static GetUsuarioByIdQueryHandler CriarHandler(AppDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_UsuarioInexistente_DeveRetornarNull()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuarioByIdQuery(Guid.NewGuid()), CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UsuarioExistente_DeveRetornarDto()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("João Admin", "joao@escola.com",
            "$2a$11$fakehash000000000000000000000000000000000000000000000000",
            PapelUsuario.Administrador);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuarioByIdQuery(usuario.Id), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(usuario.Id);
        resultado.Nome.Should().Be("João Admin");
        resultado.Email.Should().Be("joao@escola.com");
        resultado.Papel.Should().Be("Administrador");
        resultado.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UsuarioDesativado_DeveRetornarNullPorQueryFilter()
    {
        // Global Query Filter filtra usuarios inativos (Ativo == false)
        // O handler NÃO usa IgnoreQueryFilters(), então o usuário desativado não é retornado
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Inativo", "inativo@escola.com",
            "$2a$11$fakehash000000000000000000000000000000000000000000000000",
            PapelUsuario.Monitor);
        usuario.Desativar("admin");
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetUsuarioByIdQuery(usuario.Id), CancellationToken.None);

        resultado.Should().BeNull("usuário desativado é filtrado pelo Global Query Filter");
    }
}
