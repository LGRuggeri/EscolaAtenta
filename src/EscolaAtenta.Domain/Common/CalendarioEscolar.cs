using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Common;

/// <summary>
/// Calcula intervalos de datas dos períodos letivos com base no tipo de divisão
/// do ano letivo adotado pela escola.
/// </summary>
public static class CalendarioEscolar
{
    /// <summary>
    /// Retorna o intervalo de datas de um período letivo dentro de um ano.
    /// </summary>
    /// <param name="anoLetivo">Ano letivo.</param>
    /// <param name="tipoPeriodo">Tipo de divisão do ano (bimestre, trimestre, semestre).</param>
    /// <param name="periodo">Número do período (1-based).</param>
    /// <returns>Data de início e fim do período.</returns>
    public static (DateTime Inicio, DateTime Fim) ObterPeriodo(
        int anoLetivo,
        TipoPeriodoLetivo tipoPeriodo,
        int periodo)
    {
        var (quantidadePeriodos, mesesPorPeriodo) = tipoPeriodo switch
        {
            TipoPeriodoLetivo.Semestre => (2, 6),
            TipoPeriodoLetivo.Trimestre => (4, 3),
            TipoPeriodoLetivo.Bimestre => (5, 2), // 5 bimestres: 4 de 2 meses + 1 de 4 meses
            _ => throw new DomainException("Tipo de período letivo inválido.")
        };

        if (periodo < 1 || periodo > quantidadePeriodos)
            throw new DomainException($"Período inválido. O ano {tipoPeriodo.ToString().ToLower()} possui {quantidadePeriodos} períodos.");

        if (tipoPeriodo == TipoPeriodoLetivo.Bimestre)
        {
            return periodo switch
            {
                1 => (Data(anoLetivo, 1, 1), Data(anoLetivo, 2, DateTime.DaysInMonth(anoLetivo, 2))),
                2 => (Data(anoLetivo, 3, 1), Data(anoLetivo, 4, 30)),
                3 => (Data(anoLetivo, 5, 1), Data(anoLetivo, 6, 30)),
                4 => (Data(anoLetivo, 7, 1), Data(anoLetivo, 8, 31)),
                5 => (Data(anoLetivo, 9, 1), Data(anoLetivo, 12, 31)),
                _ => throw new DomainException("Período inválido.")
            };
        }

        var inicio = new DateTime(anoLetivo, (periodo - 1) * mesesPorPeriodo + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fimMes = inicio.Month + mesesPorPeriodo - 1;
        var fim = new DateTime(anoLetivo, fimMes, DateTime.DaysInMonth(anoLetivo, fimMes), 23, 59, 59, DateTimeKind.Utc);

        return (inicio, fim);
    }

    private static DateTime Data(int ano, int mes, int dia)
    {
        return new DateTime(ano, mes, dia, 0, 0, 0, DateTimeKind.Utc);
    }
}
