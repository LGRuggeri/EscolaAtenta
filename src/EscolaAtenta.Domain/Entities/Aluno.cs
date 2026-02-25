namespace EscolaAtenta.Domain.Entities;

public class Aluno(Guid id, string nome, string matricula, Guid turmaId)
{
    public Guid Id { get; private set; } = id;
    public string Nome { get; private set; } = nome;
    public string Matricula { get; private set; } = matricula;
    public Guid TurmaId { get; private set; } = turmaId;

    // Navegação
    public virtual Turma Turma { get; private set; } = null!;
    public virtual ICollection<RegistroPresenca> RegistrosPresenca { get; private set; } = [];
    public virtual ICollection<AlertaEvasao> AlertasEvasao { get; private set; } = [];
}
