using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa um alerta de risco de evasão escolar gerado para um aluno.
/// 
/// Invariantes protegidas:
/// 1. AlunoId é obrigatório e imutável.
/// 2. Um alerta nasce não resolvido (Resolvido = false).
/// 3. A resolução é feita via MarcarComoResolvido() com validação.
/// </summary>
public class AlertaEvasao : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private AlertaEvasao() { }

    public Guid? AlunoId { get; private set; }
    public Guid? TurmaId { get; private set; }
    public NivelAlertaFalta Nivel { get; private set; }
    public DateTimeOffset DataAlerta { get; private set; }

    /// <summary>
    /// Descrição contextual do alerta — inclui nome do aluno, total de faltas, etc.
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    public static AlertaEvasao CriarAlertaAluno(Guid alunoId, Guid turmaId, NivelAlertaFalta nivel, string motivo)
    {
        return new AlertaEvasao 
        { 
            AlunoId = alunoId, 
            TurmaId = turmaId,
            Nivel = nivel, 
            Descricao = motivo, 
            DataAlerta = DateTimeOffset.UtcNow, 
            Resolvido = false 
        };
    }

    public static AlertaEvasao CriarAlertaTurma(Guid turmaId, string motivo)
    {
        // Nível 0 (Excelência) para a turma
        return new AlertaEvasao 
        { 
            TurmaId = turmaId, 
            Nivel = NivelAlertaFalta.Excelencia, 
            Descricao = motivo, 
            DataAlerta = DateTimeOffset.UtcNow, 
            Resolvido = true // Já nasce resolvido pois é um badge positivo
        };
    }

    public bool Resolvido { get; private set; }
    public DateTimeOffset? DataResolucao { get; private set; }
    public string? ObservacaoResolucao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Aluno? Aluno { get; private set; }
    public virtual Turma? Turma { get; private set; }

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
