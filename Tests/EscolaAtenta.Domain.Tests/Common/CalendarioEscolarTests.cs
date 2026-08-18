using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using FluentAssertions;

namespace EscolaAtenta.Domain.Tests.Common;

public class CalendarioEscolarTests
{
    [Theory]
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 1, 1, 1, 6, 30)]
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 2, 7, 1, 12, 31)]
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 1, 1, 1, 3, 31)]
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 2, 4, 1, 6, 30)]
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 3, 7, 1, 9, 30)]
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 4, 10, 1, 12, 31)]
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 1, 1, 1, 2, 28)]
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 2, 3, 1, 4, 30)]
    [InlineData(2024, TipoPeriodoLetivo.Bimestre, 1, 1, 1, 2, 29)]
    public void ObterPeriodo_DeveRetornarIntervaloCorreto(
        int anoLetivo,
        TipoPeriodoLetivo tipo,
        int periodo,
        int mesInicio,
        int diaInicio,
        int mesFim,
        int diaFim)
    {
        var (inicio, fim) = CalendarioEscolar.ObterPeriodo(anoLetivo, tipo, periodo);

        inicio.Year.Should().Be(anoLetivo);
        inicio.Month.Should().Be(mesInicio);
        inicio.Day.Should().Be(diaInicio);
        fim.Year.Should().Be(anoLetivo);
        fim.Month.Should().Be(mesFim);
        fim.Day.Should().Be(diaFim);
    }

    [Theory]
    [InlineData(TipoPeriodoLetivo.Semestre, 0)]
    [InlineData(TipoPeriodoLetivo.Semestre, 3)]
    [InlineData(TipoPeriodoLetivo.Trimestre, 0)]
    [InlineData(TipoPeriodoLetivo.Trimestre, 5)]
    [InlineData(TipoPeriodoLetivo.Bimestre, 0)]
    [InlineData(TipoPeriodoLetivo.Bimestre, 6)]
    public void ObterPeriodo_ComPeriodoInvalido_DeveLancarDomainException(TipoPeriodoLetivo tipo, int periodo)
    {
        Action acao = () => CalendarioEscolar.ObterPeriodo(2025, tipo, periodo);

        acao.Should().Throw<DomainException>();
    }
}
