using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Usuarios.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class CriarUsuarioHandlerTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new FakeCurrentUserService(), new FakeMediator(), new FakeTenantProvider());
    }

    [Fact]
    public async Task Handle_QuandoEmailNaoExiste_DeveCriarUsuarioERetornarResultado()
    {
        await using var ctx = CriarContexto();
        var handler = new CriarUsuarioHandler(ctx);
        var command = new CriarUsuarioCommand("Maria Silva", "maria@escola.com", PapelUsuario.Monitor);

        var resultado = await handler.Handle(command, CancellationToken.None);

        resultado.Id.Should().NotBeEmpty();
        resultado.Email.Should().Be("maria@escola.com");
        resultado.SenhaInicial.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_QuandoEmailJaExiste_DeveDispararInvalidOperationException()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(new Usuario("João", "joao@escola.com", "hash_qualquer", PapelUsuario.Monitor));
        await ctx.SaveChangesAsync();

        var handler = new CriarUsuarioHandler(ctx);
        var command = new CriarUsuarioCommand("Outro", "joao@escola.com", PapelUsuario.Monitor);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*joao@escola.com*");
    }

    [Fact]
    public async Task Handle_QuandoEmailExisteComCaseDiferente_DeveDispararInvalidOperationException()
    {
        await using var ctx = CriarContexto();
        ctx.Usuarios.Add(new Usuario("João", "joao@escola.com", "hash_qualquer", PapelUsuario.Monitor));
        await ctx.SaveChangesAsync();

        var handler = new CriarUsuarioHandler(ctx);
        var command = new CriarUsuarioCommand("Outro", "JOAO@ESCOLA.COM", PapelUsuario.Monitor);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
