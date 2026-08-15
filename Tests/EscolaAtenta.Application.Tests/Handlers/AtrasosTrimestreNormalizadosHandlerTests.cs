using EscolaAtenta.Application.EventHandlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class AtrasosTrimestreNormalizadosHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static AtrasosTrimestreNormalizadosEvent CriarEvento(Guid alunoId, Guid turmaId, int atrasosNoTrimestre = 0) =>
        new(alunoId, turmaId, "Ana Souza", atrasosNoTrimestre);

    [Fact]
    public async Task Handle_QuandoExisteAlertaPendente_DeveResolverAutomaticamente()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        var alertaPendente = AlertaEvasao.CriarAlertaAtraso(alunoId, turmaId, NivelAlertaFalta.Intermediario, "6 atrasos no trimestre.");
        ctx.AlertasEvasao.Add(alertaPendente);
        await ctx.SaveChangesAsync();

        var handler = new AtrasosTrimestreNormalizadosHandler(ctx, NullLogger<AtrasosTrimestreNormalizadosHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, 0), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alerta = await ctx.AlertasEvasao.FirstAsync(a => a.AlunoId == alunoId);
        alerta.Resolvido.Should().BeTrue();
        alerta.JustificativaResolucao.Should().Contain("correção de presença");
    }

    [Fact]
    public async Task Handle_QuandoNaoExisteAlertaPendente_NaoDeveFazerNada()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        var handler = new AtrasosTrimestreNormalizadosHandler(ctx, NullLogger<AtrasosTrimestreNormalizadosHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, 0), CancellationToken.None);

        var alertas = await ctx.AlertasEvasao.ToListAsync();
        alertas.Should().BeEmpty();
    }
}
