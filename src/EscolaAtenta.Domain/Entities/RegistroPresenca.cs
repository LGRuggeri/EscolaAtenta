using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa o registro de presença de um aluno em uma chamada específica.
/// 
/// Invariantes protegidas:
/// 1. ChamadaId e AlunoId são obrigatórios e imutáveis após criação.
/// 2. O status pode ser alterado via AlterarStatus() com validação.
/// 
/// Decisão: RegistroPresenca é criado exclusivamente via Chamada.RegistrarPresenca(),
/// garantindo que a invariante de duplicidade seja sempre verificada.
/// O construtor interno (internal) impede criação direta de fora do assembly Domain.
/// </summary>
public class RegistroPresenca : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private RegistroPresenca() { }

    /// <summary>
    /// Construtor interno — apenas Chamada.RegistrarPresenca() deve criar instâncias.
    /// </summary>
    internal RegistroPresenca(Guid id, Guid chamadaId, Guid alunoId, StatusPresenca status)
        : base(id)
    {
        if (chamadaId == Guid.Empty)
            throw new DomainException("O registro de presença deve estar associado a uma chamada válida.");

        if (alunoId == Guid.Empty)
            throw new DomainException("O registro de presença deve estar associado a um aluno válido.");

        ChamadaId = chamadaId;
        AlunoId = alunoId;
        Status = status;
    }

    public Guid ChamadaId { get; private set; }
    public Guid AlunoId { get; private set; }
    public StatusPresenca Status { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Chamada Chamada { get; private set; } = null!;
    public virtual Aluno Aluno { get; private set; } = null!;

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Altera o status de presença do aluno.
    /// Útil para correções após o lançamento inicial da chamada.
    /// </summary>
    public void AlterarStatus(StatusPresenca novoStatus)
    {
        if (Status == novoStatus)
            throw new DomainException($"O status já é '{novoStatus}'. Nenhuma alteração necessária.");

        Status = novoStatus;
    }
}
