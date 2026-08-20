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
    /// o rollout OTA. O período, quando informado, é interpretado como trimestre.
    /// </summary>
    public static RelatorioTurmaQuery DePeriodoLetivo(Guid turmaId, int anoLetivo, int? periodoLetivo = null)
    {
        DateTime inicio;
        DateTime fim;

        if (periodoLetivo.HasValue)
        {
            // Interpretação legada: período = trimestre (1..4)
            var trimestre = Math.Clamp(periodoLetivo.Value, 1, 4);
            inicio = trimestre switch
            {
                1 => new DateTime(anoLetivo, 1, 1),
                2 => new DateTime(anoLetivo, 4, 1),
                3 => new DateTime(anoLetivo, 7, 1),
                4 => new DateTime(anoLetivo, 10, 1),
                _ => new DateTime(anoLetivo, 1, 1)
            };
            fim = trimestre switch
            {
                1 => new DateTime(anoLetivo, 3, 31),
                2 => new DateTime(anoLetivo, 6, 30),
                3 => new DateTime(anoLetivo, 9, 30),
                4 => new DateTime(anoLetivo, 12, 31),
                _ => new DateTime(anoLetivo, 12, 31)
            };
        }
        else
        {
            inicio = new DateTime(anoLetivo, 1, 1);
            fim = new DateTime(anoLetivo, 12, 31);
        }

        return new RelatorioTurmaQuery(turmaId, inicio, fim);
    }
}
