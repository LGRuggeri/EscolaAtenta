using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Queries;

public record RelatorioTurmaQuery(
    string TurmaId,
    DateTime DataInicio,
    DateTime DataFim) : IRequest<RelatorioTurmaDto>
{
    /// <summary>
    /// Cria uma query a partir do contrato legado (ano letivo + período letivo),
    /// preservando compatibilidade com apps Android ainda não atualizados durante
    /// o rollout OTA.
    ///
    /// O <paramref name="tipoPeriodoLetivo"/> define se periodoLetivo representa
    /// bimestre (1..5), trimestre (1..4) ou semestre (1..2).
    /// Quando periodoLetivo é omitido, usa o período corrente da data de hoje
    /// dentro da divisão configurada.
    /// </summary>
    public static RelatorioTurmaQuery DePeriodoLetivo(
        string turmaId,
        int anoLetivo,
        TipoPeriodoLetivo tipoPeriodoLetivo,
        int? periodoLetivo = null)
    {
        DateTime inicio;
        DateTime fim;

        if (periodoLetivo.HasValue)
        {
            (inicio, fim) = IntervaloPeriodoLetivo(anoLetivo, tipoPeriodoLetivo, periodoLetivo.Value);
        }
        else
        {
            var hoje = DateTime.Today;
            var periodoAtual = hoje.Year == anoLetivo
                ? PeriodoDeData(hoje, tipoPeriodoLetivo)
                : 1;
            (inicio, fim) = IntervaloPeriodoLetivo(anoLetivo, tipoPeriodoLetivo, periodoAtual);
        }

        return new RelatorioTurmaQuery(turmaId, inicio, fim);
    }

    private static int PeriodoDeData(DateTime data, TipoPeriodoLetivo tipo)
    {
        return tipo switch
        {
            TipoPeriodoLetivo.Bimestre => data.Month switch
            {
                >= 1 and <= 2 => 1,
                >= 3 and <= 4 => 2,
                >= 5 and <= 6 => 3,
                >= 7 and <= 8 => 4,
                _ => 5   // setembro a dezembro: 5º bimestre legado
            },
            TipoPeriodoLetivo.Trimestre => data.Month switch
            {
                >= 1 and <= 3 => 1,
                >= 4 and <= 6 => 2,
                >= 7 and <= 9 => 3,
                _ => 4
            },
            TipoPeriodoLetivo.Semestre => data.Month <= 6 ? 1 : 2,
            _ => PeriodoDeData(data, TipoPeriodoLetivo.Trimestre)
        };
    }

    private static (DateTime Inicio, DateTime Fim) IntervaloPeriodoLetivo(
        int ano,
        TipoPeriodoLetivo tipo,
        int periodo)
    {
        return tipo switch
        {
            TipoPeriodoLetivo.Bimestre => periodo switch
            {
                <= 1 => (new DateTime(ano, 1, 1), new DateTime(ano, 2, DateTime.DaysInMonth(ano, 2))),
                2 => (new DateTime(ano, 3, 1), new DateTime(ano, 4, 30)),
                3 => (new DateTime(ano, 5, 1), new DateTime(ano, 6, 30)),
                4 => (new DateTime(ano, 7, 1), new DateTime(ano, 8, 31)),
                >= 5 => (new DateTime(ano, 9, 1), new DateTime(ano, 12, 31))   // 5º bimestre legado: set-dez
            },
            TipoPeriodoLetivo.Trimestre => periodo switch
            {
                <= 1 => (new DateTime(ano, 1, 1), new DateTime(ano, 3, 31)),
                2 => (new DateTime(ano, 4, 1), new DateTime(ano, 6, 30)),
                3 => (new DateTime(ano, 7, 1), new DateTime(ano, 9, 30)),
                >= 4 => (new DateTime(ano, 10, 1), new DateTime(ano, 12, 31))
            },
            TipoPeriodoLetivo.Semestre => periodo switch
            {
                <= 1 => (new DateTime(ano, 1, 1), new DateTime(ano, 6, 30)),
                >= 2 => (new DateTime(ano, 7, 1), new DateTime(ano, 12, 31))
            },
            _ => IntervaloPeriodoLetivo(ano, TipoPeriodoLetivo.Trimestre, periodo)
        };
    }
}
