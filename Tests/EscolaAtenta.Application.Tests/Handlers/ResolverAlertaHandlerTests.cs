using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Application.Alertas.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class ResolverAlertaHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Handle_QuandoAlertaNaoEncontrado_DeveRetornarFalso()
    {
        await using var ctx = CriarContexto();
        var handler = new ResolverAlertaHandler(ctx, new FakeCurrentUserService());

        var resultado = await handler.Handle(
            new ResolverAlertaCommand { AlertaId = Guid.NewGuid(), Justificativa = "qualquer" },
            CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_QuandoUsuarioIdNaoEhGuid_DeveDispararUnauthorizedAccessException()
    {
        await using var ctx = CriarContexto();
        var alerta = AlertaEvasao.CriarAlertaAluno(Guid.NewGuid(), Guid.NewGuid(), NivelAlertaFalta.Aviso, "teste");
        ctx.AlertasEvasao.Add(alerta);
        await ctx.SaveChangesAsync();

        // UsuarioId não é um Guid válido
        var currentUser = new FakeCurrentUserService { UsuarioId = "nao-e-um-guid" };
        var handler = new ResolverAlertaHandler(ctx, currentUser);

        Func<Task> act = () => handler.Handle(
            new ResolverAlertaCommand { AlertaId = alerta.Id, Justificativa = "justificativa" },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_QuandoAlertaExisteEUsuarioValido_DeveResolverERetornarTrue()
    {
        await using var ctx = CriarContexto();
        var alerta = AlertaEvasao.CriarAlertaAluno(Guid.NewGuid(), Guid.NewGuid(), NivelAlertaFalta.Aviso, "teste");
        ctx.AlertasEvasao.Add(alerta);
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService { UsuarioId = Guid.NewGuid().ToString() };
        var handler = new ResolverAlertaHandler(ctx, currentUser);

        var resultado = await handler.Handle(
            new ResolverAlertaCommand { AlertaId = alerta.Id, Justificativa = "Situação normalizada." },
            CancellationToken.None);

        resultado.Should().BeTrue();
        var salvo = await ctx.AlertasEvasao.FindAsync(alerta.Id);
        salvo!.Resolvido.Should().BeTrue();
        salvo.JustificativaResolucao.Should().Be("Situação normalizada.");
    }
}
