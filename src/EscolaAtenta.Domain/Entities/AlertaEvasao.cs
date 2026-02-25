using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa um alerta de risco de evasão escolar gerado para um aluno.
/// 
/// Invariantes protegidas:
/// 1. AlunoId é obrigatório e imutável.
/// 2. Um alerta nasce não resolvido (Resolvido = false).
/// 3. A resolução é feita via MarcarComoResolvido() com validação.
/// 
/// Decisão: AlertaEvasao é criado exclusivamente pelo LimiteFaltasAtingidoHandler
/// em resposta ao Domain Event LimiteFaltasAtingidoEvent. Isso garante que
/// alertas só existam quando há uma causa rastreável no domínio.
/// </summary>
public class AlertaEvasao : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private AlertaEvasao() { }

    /// <summary>
    /// Cria um novo alerta de evasão.
    /// </summary>
    public AlertaEvasao(Guid id, Guid alunoId, string descricao)
        : base(id)
    {
        if (alunoId == Guid.Empty)
            throw new DomainException("O alerta deve estar associado a um aluno válido.");

        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição do alerta é obrigatória.");

        AlunoId = alunoId;
        Descricao = descricao;
        DataAlerta = DateTimeOffset.UtcNow;
        Resolvido = false;
    }

    public Guid AlunoId { get; private set; }
    public DateTimeOffset DataAlerta { get; private set; }

    /// <summary>
    /// Descrição contextual do alerta — inclui nome do aluno, total de faltas, etc.
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    public bool Resolvido { get; private set; }
    public DateTimeOffset? DataResolucao { get; private set; }
    public string? ObservacaoResolucao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Aluno Aluno { get; private set; } = null!;

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Marca o alerta como resolvido, registrando a observação da resolução.
    /// </summary>
    public void MarcarComoResolvido(string observacao)
    {
        if (Resolvido)
            throw new DomainException("Este alerta já foi resolvido.");

        if (string.IsNullOrWhiteSpace(observacao))
            throw new DomainException("A observação de resolução é obrigatória.");

        Resolvido = true;
        DataResolucao = DateTimeOffset.UtcNow;
        ObservacaoResolucao = observacao;
    }
}
