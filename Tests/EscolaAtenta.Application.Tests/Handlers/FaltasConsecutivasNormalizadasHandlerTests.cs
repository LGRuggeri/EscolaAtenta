using EscolaAtenta.Application.EventHandlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class FaltasConsecutivasNormalizadasHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static FaltasConsecutivasNormalizadasEvent CriarEvento(Guid alunoId, Guid turmaId, int faltasConsecutivas = 0) =>
        new(alunoId, turmaId, "Ana Souza", faltasConsecutivas);

    [Fact]
    public async Task Handle_QuandoExisteAlertaPendente_DeveResolverAutomaticamente()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        var alertaPendente = AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Vermelho, "3 faltas consecutivas.");
        ctx.AlertasEvasao.Add(alertaPendente);
        await ctx.SaveChangesAsync();

        var handler = new FaltasConsecutivasNormalizadasHandler(ctx, NullLogger<FaltasConsecutivasNormalizadasHandler>.Instance);
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

        var handler = new FaltasConsecutivasNormalizadasHandler(ctx, NullLogger<FaltasConsecutivasNormalizadasHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, 0), CancellationToken.None);

        var alertas = await ctx.AlertasEvasao.ToListAsync();
        alertas.Should().BeEmpty();
    }
}
