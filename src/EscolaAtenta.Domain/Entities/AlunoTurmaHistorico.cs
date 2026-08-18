using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa o vínculo histórico de um aluno com uma turma em um determinado
/// ano letivo. Permite rastrear mudanças de série, turno ou período sem perder
/// o histórico anterior.
/// 
/// Invariantes:
/// 1. AlunoId e TurmaId são obrigatórios.
/// 2. DataInicio é obrigatória.
/// 3. DataFim, quando informada, deve ser maior ou igual a DataInicio.
/// 4. Apenas uma matrícula pode estar ativa (DataFim nula) por aluno.
/// </summary>
public class AlunoTurmaHistorico : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private AlunoTurmaHistorico() { }

    public AlunoTurmaHistorico(
        Guid id,
        Guid alunoId,
        Guid turmaId,
        int anoLetivo,
        DateTime dataInicio,
        DateTime? dataFim,
        string? motivo)
        : base(id)
    {
        if (alunoId == Guid.Empty)
            throw new DomainException("O aluno do histórico deve ser válido.");

        if (turmaId == Guid.Empty)
            throw new DomainException("A turma do histórico deve ser válida.");

        if (anoLetivo < 2000 || anoLetivo > 2100)
            throw new DomainException("O ano letivo deve estar entre 2000 e 2100.");

        if (dataFim.HasValue && dataFim.Value < dataInicio)
            throw new DomainException("A data de fim não pode ser anterior à data de início.");

        AlunoId = alunoId;
        TurmaId = turmaId;
        AnoLetivo = anoLetivo;
        DataInicio = dataInicio;
        DataFim = dataFim;
        Motivo = motivo;
    }

    public Guid AlunoId { get; private set; }
    public Guid TurmaId { get; private set; }
    public int AnoLetivo { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }
    public string? Motivo { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Aluno Aluno { get; private set; } = null!;
    public virtual Turma Turma { get; private set; } = null!;

    /// <summary>
    /// Indica se a matrícula ainda está em vigor.
    /// </summary>
    public bool Ativa => !DataFim.HasValue;

    /// <summary>
    /// Encerra a vigência desta matrícula.
    /// </summary>
    public void Encerrar(DateTime dataFim)
    {
        if (dataFim < DataInicio)
            throw new DomainException("A data de fim não pode ser anterior à data de início.");

        if (DataFim.HasValue)
            throw new DomainException("Esta matrícula já foi encerrada.");

        DataFim = dataFim;
    }
}
