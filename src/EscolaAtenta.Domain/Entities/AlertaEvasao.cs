using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa um alerta de risco escolar gerado para um aluno (evasão ou atraso).
///
/// Decisão de design: O nome da classe permanece AlertaEvasao para evitar uma
/// refatoração massiva do banco. O campo Tipo (TipoAlerta enum) discrimina
/// a origem do alerta na UI sem criar nova tabela.
///
/// Invariantes protegidas:
/// 1. AlunoId é obrigatório e imutável.
/// 2. Um alerta nasce não resolvido (Resolvido = false).
/// 3. A resolução é feita via MarcarComoResolvido() com validação.
/// 4. A escalada de nível é feita via AtualizarNivel() — sem criar novo alerta.
/// </summary>
public class AlertaEvasao : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private AlertaEvasao() { }

    public Guid? AlunoId { get; private set; }
    public Guid? TurmaId { get; private set; }
    public NivelAlertaFalta Nivel { get; private set; }

    /// <summary>
    /// Classifica a origem do alerta: Evasao (faltas) ou Atraso.
    /// Default = Evasao (1) para compatibilidade com registros pré-existentes.
    /// </summary>
    public TipoAlerta Tipo { get; private set; } = TipoAlerta.Evasao;
    public DateTimeOffset DataAlerta { get; private set; }

    /// <summary>
    /// Descrição contextual do alerta — inclui nome do aluno, total de faltas, etc.
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>
    /// Cria um alerta de evasão para um aluno específico.
    /// </summary>
    /// <param name="alunoId">ID do aluno</param>
    /// <param name="turmaId">ID da turma</param>
    /// <param name="nivel">Nível de severidade do alerta</param>
    /// <param name="motivo">Descrição do motivo do alerta</param>
    /// <returns>Nova instância de AlertaEvasao</returns>
    public static AlertaEvasao CriarAlertaAluno(Guid alunoId, Guid turmaId, NivelAlertaFalta nivel, string motivo)
    {
        // Invariante de Domínio: O nível nunca pode ultrapassar Preto (5)
        // Garante que valores acima do máximo sejam truncados para Preto
        var nivelValidado = NivelAlertaFaltaExtensions.GarantirLimiteMaximo(nivel);

        return new AlertaEvasao 
        { 
            AlunoId = alunoId, 
            TurmaId = turmaId,
            Nivel = nivelValidado, 
            Descricao = motivo, 
            DataAlerta = DateTimeOffset.UtcNow, 
            Resolvido = false 
        };
    }

    /// <summary>
    /// Cria um alerta originado por atrasos consecutivos excessivos no trimestre.
    /// </summary>
    public static AlertaEvasao CriarAlertaAtraso(Guid alunoId, Guid turmaId, NivelAlertaFalta nivel, string motivo)
    {
        var nivelValidado = NivelAlertaFaltaExtensions.GarantirLimiteMaximo(nivel);
        return new AlertaEvasao
        {
            AlunoId = alunoId,
            TurmaId = turmaId,
            Nivel = nivelValidado,
            Descricao = motivo,
            DataAlerta = DateTimeOffset.UtcNow,
            Resolvido = false,
            Tipo = TipoAlerta.Atraso
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
    public Guid? ResolvidoPorId { get; private set; }
    public string? JustificativaResolucao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Aluno? Aluno { get; private set; }
    public virtual Turma? Turma { get; private set; }
    public virtual Usuario? ResolvidoPor { get; private set; }

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Atualiza o nível de severidade de um alerta já existente (escalada).
    /// Utilizado pelo Handler quando o aluno agrava sua situação antes da
    /// supervisão tratar o alerta anterior. Evita duplicatas no dashboard.
    /// </summary>
    public void AtualizarNivel(NivelAlertaFalta novoNivel, string novoMotivo)
    {
        if (Resolvido)
            throw new DomainException("Não é possível escalar um alerta já resolvido.");

        Nivel = NivelAlertaFaltaExtensions.GarantirLimiteMaximo(novoNivel);
        Descricao = novoMotivo;
        DataAlerta = DateTimeOffset.UtcNow; // Atualiza timestamp para ordenação correta
    }

    /// <summary>
    /// Marca o alerta como resolvido, registrando a observação da resolução.
    /// </summary>
    public void MarcarComoResolvido(Guid usuarioId, string justificativa)
    {
        if (Resolvido)
            throw new DomainException("Este alerta já foi resolvido.");

        if (string.IsNullOrWhiteSpace(justificativa))
            throw new DomainException("A observação de resolução é obrigatória.");

        Resolvido = true;
        DataResolucao = DateTimeOffset.UtcNow;
        ObservacaoResolucao = justificativa;
        JustificativaResolucao = justificativa;
        ResolvidoPorId = usuarioId;
    }
}
