using EscolaAtenta.Application.Auth;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class LoginHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public LoginHandlerTests()
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

    private static LoginHandler CriarHandler(AppDbContext ctx) =>
        new(ctx, new FakeAuthService());

    private static Usuario CriarUsuarioAtivo(string email = "monitor@escola.com") =>
        new("Monitor Teste", email, "hash-senha-correta", PapelUsuario.Monitor);

    [Fact]
    public async Task Handle_CredenciaisValidas_DeveRetornarTokenERefreshToken()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuarioAtivo());
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-correta"),
            CancellationToken.None);

        resultado.Token.Should().NotBeNullOrEmpty();
        resultado.Email.Should().Be("monitor@escola.com");
        resultado.Papel.Should().Be("Monitor");
        resultado.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_EmailInexistente_DeveDispararCredenciaisInvalidasException()
    {
        await using var ctx = CriarContexto();

        Func<Task> act = () => CriarHandler(ctx).Handle(
            new LoginCommand("naoexiste@escola.com", "qualquer"),
            CancellationToken.None);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task Handle_SenhaIncorreta_DeveDispararCredenciaisInvalidasException()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuarioAtivo());
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        Func<Task> act = () => CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-errada"),
            CancellationToken.None);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task Handle_UsuarioInativo_DeveDispararCredenciaisInvalidasException()
    {
        await using var ctx = CriarContexto();
        var usuario = CriarUsuarioAtivo();
        usuario.Desativar("admin");
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Precisa ignorar query filter pois o usuário inativo não será encontrado
        Func<Task> act = () => CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-correta"),
            CancellationToken.None);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task Handle_EmailComCaseEEspacos_DeveNormalizar()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(CriarUsuarioAtivo("user@escola.com"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new LoginCommand("  USER@ESCOLA.COM  ", "senha-correta"),
            CancellationToken.None);

        resultado.Email.Should().Be("user@escola.com");
    }

    [Fact]
    public async Task Handle_LoginBemSucedido_DeveRevogarRefreshTokensAnteriores()
    {
        await using var ctx = CriarContexto();
        var usuario = CriarUsuarioAtivo();
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        var usuarioId = usuario.Id;
        ctx.ChangeTracker.Clear();

        // Primeiro login
        await CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-correta"),
            CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var tokensAntes = await ctx.RefreshTokens
            .Where(rt => rt.UsuarioId == usuarioId && !rt.Revogado)
            .CountAsync();
        tokensAntes.Should().Be(1);

        // Segundo login — deve revogar o anterior
        await CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-correta"),
            CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var tokensAtivos = await ctx.RefreshTokens
            .Where(rt => rt.UsuarioId == usuarioId && !rt.Revogado)
            .CountAsync();
        tokensAtivos.Should().Be(1, "somente o token mais recente deve estar ativo");

        var tokensRevogados = await ctx.RefreshTokens
            .Where(rt => rt.UsuarioId == usuarioId && rt.Revogado)
            .CountAsync();
        tokensRevogados.Should().Be(1, "o token anterior deve ter sido revogado");
    }

    [Fact]
    public async Task Handle_UsuarioDeveAlterarSenha_DeveRetornarFlagTrue()
    {
        await using var ctx = CriarContexto();
        var usuario = CriarUsuarioAtivo();
        usuario.ExigirTrocaDeSenha();
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new LoginCommand("monitor@escola.com", "senha-correta"),
            CancellationToken.None);

        resultado.DeveAlterarSenha.Should().BeTrue();
    }
}
