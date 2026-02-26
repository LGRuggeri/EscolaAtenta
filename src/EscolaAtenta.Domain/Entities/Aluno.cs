using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa um aluno matriculado no sistema.
/// 
/// Invariantes protegidas:
/// 1. Nome e Matrícula são obrigatórios e têm tamanho máximo.
/// 2. Um aluno deve estar associado a uma turma válida.
/// 3. Alunos não podem ser excluídos fisicamente (ISoftDeletable).
/// 4. A verificação de limite de faltas dispara Domain Event para geração de alerta.
/// 
/// Novas Regras de Negócio (Pivot):
/// - O sistema é operado por um Monitor que passa de sala em sala.
/// - O foco é alertar a Supervisão sobre faltas consecutivas.
/// - Alerta de evasão é gerado APENAS quando FaltasConsecutivasAtuais == 3.
/// 
/// Decisão sobre Soft Delete: Alunos são dados históricos críticos.
/// Mesmo após desligamento, seus registros de presença devem ser preservados
/// para fins de auditoria e relatórios históricos.
/// </summary>
public class Aluno : EntityBase, ISoftDeletable
{
    private readonly List<RegistroPresenca> _registrosPresenca = [];
    private readonly List<AlertaEvasao> _alertasEvasao = [];

    // Construtor privado para uso exclusivo do EF Core
    private Aluno() { }

    /// <summary>
    /// Cria um novo aluno validando todas as invariantes.
    /// </summary>
    public Aluno(Guid id, string nome, string? matricula, Guid turmaId)
        : base(id)
    {
        ValidarNome(nome);

        if (turmaId == Guid.Empty)
            throw new DomainException("O aluno deve estar associado a uma turma válida.");

        Nome = nome;
        Matricula = matricula?.Trim() ?? string.Empty;
        TurmaId = turmaId;
        Ativo = true; // Todo aluno nasce ativo
        FaltasConsecutivasAtuais = 0; // Inicializa contadores de falta
        TotalFaltas = 0;
    }

    public string Nome { get; private set; } = string.Empty;
    public string Matricula { get; private set; } = string.Empty;
    public Guid TurmaId { get; private set; }

    // ── Controle de Faltas (Novas propriedades) ───────────────────────────────
    /// <summary>
    /// Número de faltas consecutivas atuais.
    /// Zera quando o aluno comparece (Presente).
    /// </summary>
    public int FaltasConsecutivasAtuais { get; private set; }

    /// <summary>
    /// Total de faltas acumuladas na história do aluno.
    /// </summary>
    public int TotalFaltas { get; private set; }

    // ── ISoftDeletable ─────────────────────────────────────────────────────────
    public bool Ativo { get; private set; }
    public DateTimeOffset? DataExclusao { get; private set; }
    public string? UsuarioExclusao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Turma Turma { get; private set; } = null!;

    public IReadOnlyCollection<RegistroPresenca> RegistrosPresenca =>
        _registrosPresenca.AsReadOnly();

    public IReadOnlyCollection<AlertaEvasao> AlertasEvasao =>
        _alertasEvasao.AsReadOnly();

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Atualiza os dados cadastrais do aluno.
    /// </summary>
    public void Atualizar(string nome, string? matricula)
    {
        ValidarNome(nome);
        Nome = nome;
        Matricula = matricula?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Transfere o aluno para outra turma.
    /// </summary>
    public void TransferirTurma(Guid novaTurmaId)
    {
        if (novaTurmaId == Guid.Empty)
            throw new DomainException("A turma de destino deve ser válida.");

        if (novaTurmaId == TurmaId)
            throw new DomainException("O aluno já pertence a esta turma.");

        TurmaId = novaTurmaId;
    }

    /// <summary>
    /// Registra a presença do aluno e atualiza os contadores de falta.
    /// 
    /// Regras de Negócio:
    /// - Se Presente: Zera FaltasConsecutivasAtuais (mantém TotalFaltas)
    /// - Se Falta: Incrementa FaltasConsecutivasAtuais E TotalFaltas
    /// - Se FaltaJustificada: Zera FaltasConsecutivasAtuais (mantém TotalFaltas)
    /// 
    /// Dispara AlertaEvasao APENAS quando FaltasConsecutivasAtuais atinge 3.
    /// </summary>
    /// <param name="status">Status da presença a ser registrado.</param>
    public void RegistrarPresenca(StatusPresenca status)
    {
        switch (status)
        {
            case StatusPresenca.Presente:
                // Presente zera a sequência de faltas
                FaltasConsecutivasAtuais = 0;
                break;

            case StatusPresenca.Falta:
                // Falta incrementa ambos contadores
                FaltasConsecutivasAtuais++;
                TotalFaltas++;
                break;

            case StatusPresenca.FaltaJustificada:
                // Falta justificada zera sequência mas conta como falta
                FaltasConsecutivasAtuais = 0;
                TotalFaltas++;
                break;

            case StatusPresenca.Ausente:
                // Ausente conta como falta sem justificar
                FaltasConsecutivasAtuais++;
                TotalFaltas++;
                break;
        }
    }

    /// <summary>
    /// Verifica se o aluno atingiu o limite de faltas consecutivas (3).
    /// Dispara o Domain Event LimiteFaltasAtingidoEvent APENAS quando
    /// FaltasConsecutivasAtuais == 3.
    /// 
    /// Novas Regras:
    /// - 1 falta: Indicativo visual apenas
    /// - 2 faltas: Atenção visual
    /// - 3+ faltas: Alerta crítico para Supervisão (DISPARA EVENTO)
    /// </summary>
    public void VerificarLimiteFaltas()
    {
        // Dispara evento APENAS quando atinge exatamente 3 faltas consecutivas
        if (FaltasConsecutivasAtuais == 3)
        {
            AddDomainEvent(new LimiteFaltasAtingidoEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                TotalFaltas: FaltasConsecutivasAtuais,
                LimiteConfigurado: 3 // Fixado em 3 conforme nova regra
            ));
        }
    }

    /// <summary>
    /// Retorna o nível de alerta baseado nas faltas consecutivas.
    /// </summary>
    public NivelAlertaFalta GetNivelAlerta()
    {
        return FaltasConsecutivasAtuais switch
        {
            0 => NivelAlertaFalta.Nenhum,
            1 => NivelAlertaFalta.Aviso,
            2 => NivelAlertaFalta.Atencao,
            _ => NivelAlertaFalta.Critico
        };
    }

    /// <summary>
    /// Realiza a exclusão lógica do aluno.
    /// O aluno não pode ser excluído fisicamente — apenas desativado.
    /// </summary>
    /// <param name="usuarioExclusao">Identificador do usuário que realizou a exclusão.</param>
    public void Desativar(string usuarioExclusao)
    {
        if (!Ativo)
            throw new DomainException("O aluno já está inativo.");

        Ativo = false;
        DataExclusao = DateTimeOffset.UtcNow;
        UsuarioExclusao = usuarioExclusao;
    }

    // ── Validações Privadas ────────────────────────────────────────────────────

    private static void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do aluno é obrigatório.");

        if (nome.Length > 200)
            throw new DomainException("O nome do aluno não pode ter mais de 200 caracteres.");
    }

    // Matrícula é opcional — validação removida por decisão de negócio
}
