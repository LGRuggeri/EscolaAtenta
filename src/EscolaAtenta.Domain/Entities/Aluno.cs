using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;

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
    public Aluno(Guid id, string nome, string matricula, Guid turmaId)
        : base(id)
    {
        ValidarNome(nome);
        ValidarMatricula(matricula);

        if (turmaId == Guid.Empty)
            throw new DomainException("O aluno deve estar associado a uma turma válida.");

        Nome = nome;
        Matricula = matricula;
        TurmaId = turmaId;
        Ativo = true; // Todo aluno nasce ativo
    }

    public string Nome { get; private set; } = string.Empty;
    public string Matricula { get; private set; } = string.Empty;
    public Guid TurmaId { get; private set; }

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
    public void Atualizar(string nome, string matricula)
    {
        ValidarNome(nome);
        ValidarMatricula(matricula);
        Nome = nome;
        Matricula = matricula;
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
    /// Verifica se o aluno atingiu o limite de faltas e, em caso positivo,
    /// dispara o Domain Event LimiteFaltasAtingidoEvent.
    /// 
    /// Decisão: A verificação é feita pelo handler após registrar a presença,
    /// passando o total de faltas já calculado. Isso evita que a entidade
    /// precise consultar o banco para contar faltas — responsabilidade da
    /// camada de Application.
    /// </summary>
    /// <param name="totalFaltas">Total de faltas do aluno na turma atual.</param>
    /// <param name="limiteConfigurado">Limite de faltas configurado para gerar alerta.</param>
    public void VerificarLimiteFaltas(int totalFaltas, int limiteConfigurado)
    {
        if (totalFaltas < 0)
            throw new DomainException("O total de faltas não pode ser negativo.");

        if (limiteConfigurado <= 0)
            throw new DomainException("O limite de faltas deve ser maior que zero.");

        // Dispara evento apenas quando o limite é atingido exatamente,
        // evitando múltiplos alertas para o mesmo aluno
        if (totalFaltas == limiteConfigurado)
        {
            AddDomainEvent(new LimiteFaltasAtingidoEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                TotalFaltas: totalFaltas,
                LimiteConfigurado: limiteConfigurado
            ));
        }
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

    private static void ValidarMatricula(string matricula)
    {
        if (string.IsNullOrWhiteSpace(matricula))
            throw new DomainException("A matrícula do aluno é obrigatória.");

        if (matricula.Length > 50)
            throw new DomainException("A matrícula não pode ter mais de 50 caracteres.");
    }
}
