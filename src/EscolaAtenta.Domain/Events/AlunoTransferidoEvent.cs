using EscolaAtenta.Domain.Common;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando um aluno é transferido de uma turma
/// para outra. Utilizado para auditoria e notificações futuras.
/// </summary>
public sealed record AlunoTransferidoEvent(
    Guid AlunoId,
    string NomeAluno,
    Guid TurmaOrigemId,
    Guid TurmaDestinoId,
    DateTime DataTransferencia,
    string? Motivo
) : IDomainEvent
{
    /// <summary>Momento em que a transferência ocorreu, sempre em UTC.</summary>
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
