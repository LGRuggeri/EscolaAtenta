namespace EscolaAtenta.Domain.Entities;

public class AlertaEvasao(Guid id, Guid alunoId, DateTimeOffset dataAlerta, bool resolvido)
{
    public Guid Id { get; private set; } = id;
    public Guid AlunoId { get; private set; } = alunoId;
    public DateTimeOffset DataAlerta { get; private set; } = dataAlerta;
    public bool Resolvido { get; private set; } = resolvido;

    // Navegação
    public virtual Aluno Aluno { get; private set; } = null!;
}
