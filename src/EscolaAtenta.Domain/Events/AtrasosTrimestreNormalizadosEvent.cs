using EscolaAtenta.Domain.Common;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando o recálculo de estatísticas deixa o aluno
/// com menos de 3 atrasos no trimestre, indicando que um alerta de atraso
/// previamente pendente deve ser resolvido automaticamente.
/// </summary>
public sealed record AtrasosTrimestreNormalizadosEvent(
    Guid AlunoId,
    Guid TurmaId,
    string NomeAluno,
    int AtrasosNoTrimestre
) : IDomainEvent
{
    /// <summary>Momento em que o alerta foi normalizado, sempre em UTC.</summary>
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
