using EscolaAtenta.Application.EventHandlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class LimiteFaltasAtingidoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static LimiteFaltasAtingidoEvent CriarEvento(Guid alunoId, Guid turmaId, NivelAlertaFalta nivel = NivelAlertaFalta.Aviso) =>
        new(alunoId, turmaId, "Ana Souza", TotalFaltas: 1, LimiteConfigurado: 5, "1 falta consecutiva.", nivel);

    [Fact]
    public async Task Handle_QuandoNaoExisteAlertaPendente_DeveCriarNovoAlerta()
    {
        await using var ctx = CriarContexto();
        var handler = new LimiteFaltasAtingidoHandler(ctx, NullLogger<LimiteFaltasAtingidoHandler>.Instance);
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Aviso), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alerta = await ctx.AlertasEvasao.FirstOrDefaultAsync(a => a.AlunoId == alunoId);
        alerta.Should().NotBeNull();
        alerta!.Resolvido.Should().BeFalse();
        alerta.Tipo.Should().Be(TipoAlerta.Evasao);
        alerta.Nivel.Should().Be(NivelAlertaFalta.Aviso);
    }

    [Fact]
    public async Task Handle_QuandoExisteAlertaPendente_DeveEscalarNivelSemCriarNovo()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        // Alerta pré-existente de nível Aviso
        var alertaExistente = AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "1 falta.");
        ctx.AlertasEvasao.Add(alertaExistente);
        await ctx.SaveChangesAsync();

        var handler = new LimiteFaltasAtingidoHandler(ctx, NullLogger<LimiteFaltasAtingidoHandler>.Instance);

        // Novo evento com nível maior (escalada)
        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Intermediario), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alertas = await ctx.AlertasEvasao.Where(a => a.AlunoId == alunoId).ToListAsync();
        alertas.Should().HaveCount(1); // Sem duplicata
        alertas[0].Nivel.Should().Be(NivelAlertaFalta.Intermediario);
    }

    [Fact]
    public async Task Handle_QuandoAlertaJaResolvido_DeveCriarNovoAlertaAoInvesDeEscalar()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        // Alerta resolvido não conta para idempotência
        var alertaResolvido = AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Antigo.");
        alertaResolvido.MarcarComoResolvido(usuarioId, "Situação normalizada.");
        ctx.AlertasEvasao.Add(alertaResolvido);
        await ctx.SaveChangesAsync();

        var handler = new LimiteFaltasAtingidoHandler(ctx, NullLogger<LimiteFaltasAtingidoHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Aviso), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alertas = await ctx.AlertasEvasao.Where(a => a.AlunoId == alunoId).ToListAsync();
        alertas.Should().HaveCount(2); // Um resolvido + um novo
        alertas.Count(a => !a.Resolvido).Should().Be(1);
    }
}
