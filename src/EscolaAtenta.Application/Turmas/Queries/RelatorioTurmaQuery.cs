using EscolaAtenta.Application.Turmas.DTOs;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Queries;

public record RelatorioTurmaQuery(
    Guid TurmaId,
    DateTime DataInicio,
    DateTime DataFim) : IRequest<RelatorioTurmaDto>
{
    /// <summary>
    /// Cria uma query a partir do contrato legado (ano letivo + período letivo),
    /// preservando compatibilidade com apps Android ainda não atualizados durante
    /// o rollout OTA.
    ///
    /// Quando periodoLetivo é informado, ele é interpretado como trimestre (1..4).
    /// Quando omitido, usa o trimestre corrente da data de hoje — equivalente
    /// ao antigo comportamento de "período letivo atual da escola", sem depender
    /// da entidade ConfiguracaoEscola removida. Isso evita que clientes legados
    /// recebam silenciosamente o ano letivo inteiro após o OTA.
    /// </summary>
    public static RelatorioTurmaQuery DePeriodoLetivo(Guid turmaId, int anoLetivo, int? periodoLetivo = null)
    {
        DateTime inicio;
        DateTime fim;

        if (periodoLetivo.HasValue)
        {
            // Interpretação legada: período = trimestre (1..4)
            var trimestre = Math.Clamp(periodoLetivo.Value, 1, 4);
            (inicio, fim) = IntervaloTrimestre(anoLetivo, trimestre);
        }
        else
        {
            // Período omitido: simula "período letivo atual" usando o trimestre de hoje.
            var hoje = DateTime.Today;
            var trimestreAtual = hoje.Year == anoLetivo
                ? TrimestreDeData(hoje)
                : 1;
            (inicio, fim) = IntervaloTrimestre(anoLetivo, trimestreAtual);
        }

        return new RelatorioTurmaQuery(turmaId, inicio, fim);
    }

    private static int TrimestreDeData(DateTime data)
    {
        return data.Month switch
        {
            >= 1 and <= 3 => 1,
            >= 4 and <= 6 => 2,
            >= 7 and <= 9 => 3,
            _ => 4
        };
    }

    private static (DateTime Inicio, DateTime Fim) IntervaloTrimestre(int ano, int trimestre)
    {
        return trimestre switch
        {
            1 => (new DateTime(ano, 1, 1), new DateTime(ano, 3, 31)),
            2 => (new DateTime(ano, 4, 1), new DateTime(ano, 6, 30)),
            3 => (new DateTime(ano, 7, 1), new DateTime(ano, 9, 30)),
            4 => (new DateTime(ano, 10, 1), new DateTime(ano, 12, 31)),
            _ => (new DateTime(ano, 1, 1), new DateTime(ano, 3, 31))
        };
    }
}
