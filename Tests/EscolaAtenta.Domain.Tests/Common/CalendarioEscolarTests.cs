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

    [Theory]
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 1, 15, 1)]    // 15/01 -> 1º semestre
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 8, 20, 2)]    // 20/08 -> 2º semestre
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 5, 10, 2)]   // 10/05 -> 2º trimestre
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 11, 5, 4)]   // 05/11 -> 4º trimestre
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 2, 14, 1)]   // 14/02 -> 1º bimestre
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 10, 1, 5)]    // 01/10 -> 5º bimestre
    public void ObterPeriodoAtual_DeveRetornarPeriodoCorreto(
        int anoLetivo,
        TipoPeriodoLetivo tipo,
        int mes,
        int dia,
        int periodoEsperado)
    {
        var data = new DateTime(anoLetivo, mes, dia, 0, 0, 0, DateTimeKind.Utc);

        var resultado = CalendarioEscolar.ObterPeriodoAtual(data, tipo, anoLetivo);

        resultado.Should().Be(periodoEsperado);
    }

    [Theory]
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 1, 15, 1)]    // Apenas 1º semestre iniciado
    [InlineData(2025, TipoPeriodoLetivo.Semestre, 8, 15, 2)]    // Ambos iniciados
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 5, 10, 2)]   // 1º e 2º trimestres iniciados
    [InlineData(2025, TipoPeriodoLetivo.Trimestre, 11, 5, 4)]   // Todos iniciados
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 2, 14, 1)]   // Apenas 1º bimestre iniciado
    [InlineData(2025, TipoPeriodoLetivo.Bimestre, 4, 1, 2)]     // 1º e 2º bimestres iniciados
    public void ListarPeriodosAteData_DeveRetornarPeriodosIniciados(
        int anoLetivo,
        TipoPeriodoLetivo tipo,
        int mes,
        int dia,
        int quantidadeEsperada)
    {
        var data = new DateTime(anoLetivo, mes, dia, 0, 0, 0, DateTimeKind.Utc);

        var resultado = CalendarioEscolar.ListarPeriodosAteData(data, tipo, anoLetivo);

        resultado.Should().HaveCount(quantidadeEsperada);
        resultado.Should().BeEquivalentTo(Enumerable.Range(1, quantidadeEsperada));
    }

    [Fact]
    public void ObterPeriodoAtual_QuandoDataAnteriorAoAno_DeveRetornarPrimeiroPeriodo()
    {
        var data = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var resultado = CalendarioEscolar.ObterPeriodoAtual(data, TipoPeriodoLetivo.Trimestre, 2025);

        resultado.Should().Be(1);
    }

    [Fact]
    public void ObterPeriodoAtual_QuandoDataPosteriorAoAno_DeveRetornarUltimoPeriodo()
    {
        var data = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var resultado = CalendarioEscolar.ObterPeriodoAtual(data, TipoPeriodoLetivo.Bimestre, 2025);

        resultado.Should().Be(5);
    }
}
