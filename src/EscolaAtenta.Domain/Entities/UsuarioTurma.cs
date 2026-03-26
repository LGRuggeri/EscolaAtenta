using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa o vínculo entre um Usuário (Monitor/Supervisão) e uma Turma.
/// Usado para validação de ownership (IDOR) — garante que apenas usuários
/// vinculados a uma turma possam operar sobre ela.
/// </summary>
public class UsuarioTurma : EntityBase
{
    private UsuarioTurma() { }

    public UsuarioTurma(Guid id, Guid usuarioId, Guid turmaId)
        : base(id)
    {
        if (usuarioId == Guid.Empty)
            throw new DomainException("O UsuarioId é obrigatório.");

        if (turmaId == Guid.Empty)
            throw new DomainException("O TurmaId é obrigatório.");

        UsuarioId = usuarioId;
        TurmaId = turmaId;
    }

    public Guid UsuarioId { get; private set; }
    public Guid TurmaId { get; private set; }

    // Navegação
    public virtual Usuario Usuario { get; private set; } = null!;
    public virtual Turma Turma { get; private set; } = null!;
}
