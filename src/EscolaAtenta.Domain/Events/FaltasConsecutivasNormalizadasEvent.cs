using EscolaAtenta.Domain.Common;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando o recálculo de estatísticas deixa o aluno
/// com zero faltas consecutivas atuais, indicando que um alerta de evasão
/// previamente pendente deve ser resolvido automaticamente.
/// </summary>
public sealed record FaltasConsecutivasNormalizadasEvent(
    Guid AlunoId,
    Guid TurmaId,
    string NomeAluno,
    int FaltasConsecutivasAtuais
) : IDomainEvent
{
    /// <summary>Momento em que o alerta foi normalizado, sempre em UTC.</summary>
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
