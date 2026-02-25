namespace EscolaAtenta.Domain.Entities;

public class Turma(Guid id, string nome, string turno, int anoLetivo)
{
    public Guid Id { get; private set; } = id;
    public string Nome { get; private set; } = nome;
    public string Turno { get; private set; } = turno;
    public int AnoLetivo { get; private set; } = anoLetivo;

    // Navegação
    public virtual ICollection<Aluno> Alunos { get; private set; } = [];
    public virtual ICollection<Chamada> Chamadas { get; private set; } = [];
}
