using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class CriarTurmaHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoComandoValido_DeveCriarTurmaERetornarDto()
    {
        await using var ctx = CriarContexto();
        var handler = new CriarTurmaHandler(ctx);

        var resultado = await handler.Handle(
            new CriarTurmaCommand("2º Ano B", "Tarde", 2026),
            CancellationToken.None);

        resultado.Id.Should().NotBeEmpty();
        resultado.Nome.Should().Be("2º Ano B");
        resultado.Turno.Should().Be("Tarde");
        resultado.AnoLetivo.Should().Be(2026);

        var salvo = await ctx.Turmas.FindAsync(resultado.Id);
        salvo.Should().NotBeNull();
    }
}
