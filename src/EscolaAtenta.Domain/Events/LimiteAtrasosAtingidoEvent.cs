using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando um aluno atinge o limite de atrasos
/// acumulados no trimestre, via Aluno.RegistrarAtraso().
/// 
/// Thresholds (limiares explícitos para evitar falhas por saltos de contador):
/// - 3 atrasos → NivelAlertaFalta.Aviso (comunicar ao aluno)
/// - 6 atrasos → NivelAlertaFalta.Intermediario (comunicar aos pais)
/// 
/// Processado pelo LimiteAtrasosAtingidoHandler na camada Application,
/// que cria o AlertaEvasao (Tipo = Atraso) de forma desacoplada.
/// </summary>
public sealed record LimiteAtrasosAtingidoEvent(
    Guid AlunoId,
    Guid TurmaId,
    string NomeAluno,
    int TotalAtrasos,
    string MotivoExato,
    NivelAlertaFalta Nivel
) : IDomainEvent
{
    /// <summary>Momento em que o limite foi atingido, sempre em UTC.</summary>
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
