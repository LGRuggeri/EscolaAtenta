using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EscolaAtenta.API.Authentication;
using EscolaAtenta.API.Middleware;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EscolaAtenta.Application.Tests.Security;

public class SessaoHttpTests
{
    [Theory]
    [InlineData("desativar")]
    [InlineData("papel")]
    [InlineData("senha")]
    public async Task TokenAnteriorAMudanca_DeveRetornar401(string mudanca)
    {
        await using var host = await Host.Criar();
        (await host.Client.GetAsync("/dados")).StatusCode.Should().Be(HttpStatusCode.OK);
        if (mudanca == "desativar") host.Usuario.Desativar("teste");
        if (mudanca == "papel") host.Usuario.AtualizarPerfil("Teste", PapelUsuario.Monitor);
        if (mudanca == "senha") host.Usuario.AlterarSenha("novo-hash-sintetico");
        await host.Db.SaveChangesAsync();
        (await host.Client.GetAsync("/dados")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TrocaPendente_BloqueiaDadosMasPermiteRotaDeTroca()
    {
        await using var host = await Host.Criar(true);
        var resposta = await host.Client.GetAsync("/dados");
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("troca_senha_obrigatoria");
        (await host.Client.PostAsync("/trocar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed class Host : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required Usuario Usuario { get; init; }
        public required HttpClient Client { get; init; }
        public static async Task<Host> Criar(bool pendente = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options,
                new FakeCurrentUserService(), new FakeMediator(), new FakeTenantProvider());
            await db.Database.EnsureCreatedAsync();
            var usuario = new Usuario("Teste", "http@example.test", "hash-sintetico", PapelUsuario.Administrador);
            if (pendente) usuario.ExigirTrocaDeSenha();
            db.Usuarios.Add(usuario); await db.SaveChangesAsync();
            var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:SecretKey"] = key }).Build();
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(db);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
                o.TokenValidationParameters = new TokenValidationParameters {
                    ValidateIssuer = true, ValidIssuer = "EscolaAtenta", ValidateAudience = true, ValidAudience = "EscolaAtenta",
                    ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
                o.Events = new JwtBearerEvents { OnTokenValidated = JwtSessionValidation.ValidarAsync };
            });
            builder.Services.AddAuthorization();
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<TrocaSenhaObrigatoriaMiddleware>();
            app.MapGet("/dados", () => Results.Ok()).RequireAuthorization();
            app.MapPost("/trocar", () => Results.NoContent()).RequireAuthorization().WithMetadata(new PermitirTrocaPendenteAttribute());
            await app.StartAsync();
            var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new AuthService(config).GerarToken(usuario).Token);
            return new Host { App=app, Connection=connection, Db=db, Usuario=usuario, Client=client };
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
