using EscolaAtenta.Application.EventHandlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class LimiteAtrasosAtingidoHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    private static LimiteAtrasosAtingidoEvent CriarEvento(Guid alunoId, Guid turmaId, NivelAlertaFalta nivel = NivelAlertaFalta.Aviso) =>
        new(alunoId, turmaId, "Carlos Lima", TotalAtrasos: 3, "3 atrasos no trimestre.", nivel);

    [Fact]
    public async Task Handle_QuandoNaoExisteAlertaPendente_DeveCriarNovoAlertaDeAtraso()
    {
        await using var ctx = CriarContexto();
        var handler = new LimiteAtrasosAtingidoHandler(ctx, NullLogger<LimiteAtrasosAtingidoHandler>.Instance);
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Aviso), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alerta = await ctx.AlertasEvasao.FirstOrDefaultAsync(a => a.AlunoId == alunoId);
        alerta.Should().NotBeNull();
        alerta!.Resolvido.Should().BeFalse();
        alerta.Tipo.Should().Be(TipoAlerta.Atraso);
        alerta.Nivel.Should().Be(NivelAlertaFalta.Aviso);
    }

    [Fact]
    public async Task Handle_QuandoExisteAlertaAtrasoNaoResolvido_DeveEscalarNivelSemCriarNovo()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        var alertaExistente = AlertaEvasao.CriarAlertaAtraso(alunoId, turmaId, NivelAlertaFalta.Aviso, "3 atrasos.");
        ctx.AlertasEvasao.Add(alertaExistente);
        await ctx.SaveChangesAsync();

        var handler = new LimiteAtrasosAtingidoHandler(ctx, NullLogger<LimiteAtrasosAtingidoHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Intermediario), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alertas = await ctx.AlertasEvasao.Where(a => a.AlunoId == alunoId).ToListAsync();
        alertas.Should().HaveCount(1);
        alertas[0].Nivel.Should().Be(NivelAlertaFalta.Intermediario);
    }

    [Fact]
    public async Task Handle_AlertaEvasaoNaoInterfereCom_AlertaDeAtraso()
    {
        await using var ctx = CriarContexto();
        var alunoId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();

        // Alerta de Evasão pendente NÃO deve ser escalado — é tipo diferente
        var alertaEvasao = AlertaEvasao.CriarAlertaAluno(alunoId, turmaId, NivelAlertaFalta.Aviso, "Faltas.");
        ctx.AlertasEvasao.Add(alertaEvasao);
        await ctx.SaveChangesAsync();

        var handler = new LimiteAtrasosAtingidoHandler(ctx, NullLogger<LimiteAtrasosAtingidoHandler>.Instance);
        await handler.Handle(CriarEvento(alunoId, turmaId, NivelAlertaFalta.Aviso), CancellationToken.None);
        await ctx.SaveChangesAsync();

        var alertas = await ctx.AlertasEvasao.Where(a => a.AlunoId == alunoId).ToListAsync();
        alertas.Should().HaveCount(2); // Evasão + Atraso separados
        alertas.Count(a => a.Tipo == TipoAlerta.Atraso).Should().Be(1);
        alertas.Count(a => a.Tipo == TipoAlerta.Evasao).Should().Be(1);
    }
}
