namespace EscolaAtenta.Domain.Entities;

public class Chamada(Guid id, DateTimeOffset dataHora, Guid turmaId, Guid responsavelId)
{
    public Guid Id { get; private set; } = id;
    public DateTimeOffset DataHora { get; private set; } = dataHora;
    public Guid TurmaId { get; private set; } = turmaId;
    public Guid ResponsavelId { get; private set; } = responsavelId;

    // Navegação
    public virtual Turma Turma { get; private set; } = null!;
    public virtual ICollection<RegistroPresenca> RegistrosPresenca { get; private set; } = [];
}
