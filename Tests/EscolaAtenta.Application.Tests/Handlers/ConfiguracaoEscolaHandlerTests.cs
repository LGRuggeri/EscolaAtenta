using EscolaAtenta.Application.Escola.Commands;
using EscolaAtenta.Application.Escola.DTOs;
using EscolaAtenta.Application.Escola.Handlers;
using EscolaAtenta.Application.Escola.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class ConfiguracaoEscolaHandlerTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

    [Fact]
    public async Task Obter_QuandoNaoExisteConfiguracao_DeveRetornarTrimestrePadrao()
    {
        await using var ctx = CriarContexto();
        var handler = new ObterConfiguracaoEscolaHandler(ctx);

        var resultado = await handler.Handle(new ObterConfiguracaoEscolaQuery(), CancellationToken.None);

        resultado.TipoPeriodoLetivo.Should().Be(TipoPeriodoLetivo.Trimestre);
    }

    [Fact]
    public async Task Obter_QuandoExisteConfiguracao_DeveRetornarTipoCorreto()
    {
        await using var ctx = CriarContexto();
        ctx.ConfiguracoesEscola.Add(new ConfiguracaoEscola(Guid.NewGuid(), TipoPeriodoLetivo.Semestre));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new ObterConfiguracaoEscolaHandler(ctx);
        var resultado = await handler.Handle(new ObterConfiguracaoEscolaQuery(), CancellationToken.None);

        resultado.TipoPeriodoLetivo.Should().Be(TipoPeriodoLetivo.Semestre);
    }

    [Fact]
    public async Task Atualizar_QuandoExisteConfiguracao_DeveAlterarTipoPeriodo()
    {
        await using var ctx = CriarContexto();
        var configuracao = new ConfiguracaoEscola(Guid.NewGuid(), TipoPeriodoLetivo.Trimestre);
        ctx.ConfiguracoesEscola.Add(configuracao);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var handler = new AtualizarConfiguracaoEscolaHandler(ctx, new FakeCurrentUserService());
        await handler.Handle(new AtualizarConfiguracaoEscolaCommand(TipoPeriodoLetivo.Bimestre), CancellationToken.None);

        var atualizada = await ctx.ConfiguracoesEscola.FindAsync(configuracao.Id);
        atualizada!.TipoPeriodoLetivo.Should().Be(TipoPeriodoLetivo.Bimestre);
    }
}
