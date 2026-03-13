using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Usuarios.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class AlternarStatusUsuarioHandlerTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new FakeCurrentUserService(), new FakeMediator(), new FakeTenantProvider());
    }

    [Fact]
    public async Task Handle_QuandoUsuarioAtivo_DeveDesativar()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Ana", "ana@escola.com", "hash", PapelUsuario.Monitor);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var handler = new AlternarStatusUsuarioHandler(ctx, new FakeCurrentUserService());
        await handler.Handle(new AlternarStatusUsuarioCommand(usuario.Id), CancellationToken.None);

        var salvo = await ctx.Usuarios.IgnoreQueryFilters().FirstAsync(u => u.Id == usuario.Id);
        salvo.Ativo.Should().BeFalse();
        salvo.DataExclusao.Should().NotBeNull();
        salvo.UsuarioExclusao.Should().Be("admin-teste");
    }

    [Fact]
    public async Task Handle_QuandoUsuarioInativo_DeveReativar()
    {
        await using var ctx = CriarContexto();
        var usuario = new Usuario("Ana", "ana@escola.com", "hash", PapelUsuario.Monitor);
        usuario.Desativar("admin-teste");
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var handler = new AlternarStatusUsuarioHandler(ctx, new FakeCurrentUserService());
        await handler.Handle(new AlternarStatusUsuarioCommand(usuario.Id), CancellationToken.None);

        var salvo = await ctx.Usuarios.IgnoreQueryFilters().FirstAsync(u => u.Id == usuario.Id);
        salvo.Ativo.Should().BeTrue();
        salvo.DataExclusao.Should().BeNull();
        salvo.UsuarioExclusao.Should().BeNull();
    }

    [Fact]
    public async Task Handle_QuandoUsuarioNaoEncontrado_DeveDispararKeyNotFoundException()
    {
        await using var ctx = CriarContexto();
        var handler = new AlternarStatusUsuarioHandler(ctx, new FakeCurrentUserService());

        Func<Task> act = () => handler.Handle(new AlternarStatusUsuarioCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
