using System.IdentityModel.Tokens.Jwt;
using EscolaAtenta.API.Controllers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.Application.Tests.Security;

public class SegurancaSessaoTests
{
    private static AuthService Auth() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["Jwt:SecretKey"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
    }).Build());

    [Fact]
    public void Token_DeveConterEstadoDeTrocaEIdentificadorDaVersaoDeSessao()
    {
        var usuario = new Usuario("Teste", "sessao@example.test", "hash-sintetico", PapelUsuario.Administrador);
        usuario.ExigirTrocaDeSenha();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(Auth().GerarToken(usuario).Token);
        token.Claims.Should().Contain(c => c.Type == "deve_alterar_senha" && c.Value == "true");
        token.Claims.Should().Contain(c => c.Type == "versao_sessao" && c.Value.Length > 20);
    }

    [Fact]
    public void Emissor_SemChaveConfigurada_NaoDeveUsarSegredoConhecido()
    {
        var auth = new AuthService(new ConfigurationBuilder().Build());
        var usuario = new Usuario("Teste", "sessao@example.test", "hash-sintetico", PapelUsuario.Monitor);
        var agir = () => auth.GerarToken(usuario);
        agir.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task RefreshAnteriorATrocaDeSenha_DeveSerRejeitado()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        var user = new Usuario("Teste", "sessao@example.test", "hash-anterior", PapelUsuario.Administrador);
        var current = new FakeCurrentUserService { UsuarioId=user.Id.ToString() };
        await using var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options,current,new FakeMediator(),new FakeTenantProvider());
        await ctx.Database.EnsureCreatedAsync();
        ctx.Usuarios.Add(user); var rt=new RefreshToken { UsuarioId=user.Id, Token=Guid.NewGuid().ToString(),ExpiraEm=DateTimeOffset.UtcNow.AddDays(1)}; ctx.RefreshTokens.Add(rt);
        await ctx.SaveChangesAsync(); ctx.ChangeTracker.Clear();
        var controller=new AuthController(new FakeMediator(),NullLogger<AuthController>.Instance,ctx,Auth(),new FakeCurrentUserService { UsuarioId=user.Id.ToString() });
        (await controller.TrocarSenha(new TrocarSenhaRequest("SenhaNovaSintetica-2026"),default)).Should().BeOfType<NoContentResult>();
        ctx.ChangeTracker.Clear();
        (await controller.Refresh(new RefreshRequest(rt.Token),default)).Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_DevePreservarTrocaObrigatoria()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        var user=new Usuario("Teste", "sessao@example.test", "hash-sintetico", PapelUsuario.Administrador); user.ExigirTrocaDeSenha();
        var current=new FakeCurrentUserService{UsuarioId=user.Id.ToString()};
        await using var ctx=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options,current,new FakeMediator(),new FakeTenantProvider()); await ctx.Database.EnsureCreatedAsync();
        ctx.Usuarios.Add(user); var rt=new RefreshToken{UsuarioId=user.Id,Token=Guid.NewGuid().ToString(),ExpiraEm=DateTimeOffset.UtcNow.AddDays(1)};ctx.RefreshTokens.Add(rt);await ctx.SaveChangesAsync();ctx.ChangeTracker.Clear();
        var controller=new AuthController(new FakeMediator(),NullLogger<AuthController>.Instance,ctx,Auth(),new FakeCurrentUserService { UsuarioId=user.Id.ToString() });
        var r=(OkObjectResult)await controller.Refresh(new RefreshRequest(rt.Token),default);
        ((EscolaAtenta.Application.Auth.LoginResponse)r.Value!).DeveAlterarSenha.Should().BeTrue();
    }
}
