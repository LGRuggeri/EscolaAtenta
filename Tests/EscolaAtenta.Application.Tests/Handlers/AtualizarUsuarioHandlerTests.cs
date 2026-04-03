using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Usuarios.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class AtualizarUsuarioHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AtualizarUsuarioHandlerTests()
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

    private static AtualizarUsuarioHandler CriarHandler(AppDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_UsuarioInexistente_DeveLancarKeyNotFoundException()
    {
        await using var ctx = CriarContexto();

        var command = new AtualizarUsuarioCommand(Guid.NewGuid(), "Novo Nome", PapelUsuario.Monitor);

        Func<Task> act = () => CriarHandler(ctx).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task Handle_ComandoValido_DeveAtualizarNomeEPapel()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Nome Antigo", "user@escola.com",
            "$2a$11$fakehash000000000000000000000000000000000000000000000000",
            PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarUsuarioCommand(usuario.Id, "Nome Novo", PapelUsuario.Administrador);
        await CriarHandler(ctx).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var salvo = await ctx.Usuarios.FindAsync(usuario.Id);
        salvo!.Nome.Should().Be("Nome Novo");
        salvo.Papel.Should().Be(PapelUsuario.Administrador);
    }

    [Fact]
    public async Task Handle_NomeVazio_DeveLancarArgumentException()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Original", "original@escola.com",
            "$2a$11$fakehash000000000000000000000000000000000000000000000000",
            PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new AtualizarUsuarioCommand(usuario.Id, "", PapelUsuario.Monitor);

        Func<Task> act = () => CriarHandler(ctx).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Nome*");
    }

    [Fact]
    public async Task Handle_EmailNaoDeveSerAlterado()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Teste", "original@escola.com",
            "$2a$11$fakehash000000000000000000000000000000000000000000000000",
            PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Atualiza nome e papel — email deve permanecer inalterado
        var command = new AtualizarUsuarioCommand(usuario.Id, "Novo Nome", PapelUsuario.Administrador);
        await CriarHandler(ctx).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var salvo = await ctx.Usuarios.FindAsync(usuario.Id);
        salvo!.Email.Should().Be("original@escola.com");
    }
}
