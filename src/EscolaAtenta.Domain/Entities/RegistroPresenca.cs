using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

public class RegistroPresenca(Guid id, Guid chamadaId, Guid alunoId, StatusPresenca status)
{
    public Guid Id { get; private set; } = id;
    public Guid ChamadaId { get; private set; } = chamadaId;
    public Guid AlunoId { get; private set; } = alunoId;
    public StatusPresenca Status { get; private set; } = status;

    // Navegação
    public virtual Chamada Chamada { get; private set; } = null!;
    public virtual Aluno Aluno { get; private set; } = null!;
}
