using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa uma turma escolar.
/// 
/// Invariantes protegidas:
/// 1. Nome, Turno e AnoLetivo são obrigatórios.
/// 2. Turmas não podem ser excluídas fisicamente (ISoftDeletable).
/// 3. Alunos e Chamadas são acessíveis apenas via IReadOnlyCollection.
/// 
/// Decisão sobre Soft Delete: Turmas são dados históricos. Mesmo após o
/// encerramento do ano letivo, os registros de chamada e presença devem
/// ser preservados para relatórios e auditoria.
/// </summary>
public class Turma : EntityBase, ISoftDeletable
{
    private readonly List<Aluno> _alunos = [];
    private readonly List<Chamada> _chamadas = [];

    // Construtor privado para uso exclusivo do EF Core
    private Turma() { }

    /// <summary>
    /// Cria uma nova turma validando todas as invariantes.
    /// </summary>
    public Turma(Guid id, string nome, string turno, int anoLetivo)
        : base(id)
    {
        ValidarNome(nome);
        ValidarTurno(turno);
        ValidarAnoLetivo(anoLetivo);

        Nome = nome;
        Turno = turno;
        AnoLetivo = anoLetivo;
        Ativo = true;
    }

    public string Nome { get; private set; } = string.Empty;
    public string Turno { get; private set; } = string.Empty;
    public int AnoLetivo { get; private set; }

    // ── ISoftDeletable ─────────────────────────────────────────────────────────
    public bool Ativo { get; private set; }
    public DateTimeOffset? DataExclusao { get; private set; }
    public string? UsuarioExclusao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public IReadOnlyCollection<Aluno> Alunos => _alunos.AsReadOnly();
    public IReadOnlyCollection<Chamada> Chamadas => _chamadas.AsReadOnly();

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Atualiza os dados da turma.
    /// </summary>
    public void Atualizar(string nome, string turno, int anoLetivo)
    {
        ValidarNome(nome);
        ValidarTurno(turno);
        ValidarAnoLetivo(anoLetivo);

        Nome = nome;
        Turno = turno;
        AnoLetivo = anoLetivo;
    }

    /// <summary>
    /// Realiza a exclusão lógica da turma.
    /// </summary>
    public void Desativar(string usuarioExclusao)
    {
        if (!Ativo)
            throw new DomainException("A turma já está inativa.");

        Ativo = false;
        DataExclusao = DateTimeOffset.UtcNow;
        UsuarioExclusao = usuarioExclusao;
    }

    // ── Validações Privadas ────────────────────────────────────────────────────

    private static void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da turma é obrigatório.");

        if (nome.Length > 200)
            throw new DomainException("O nome da turma não pode ter mais de 200 caracteres.");
    }

    private static void ValidarTurno(string turno)
    {
        if (string.IsNullOrWhiteSpace(turno))
            throw new DomainException("O turno da turma é obrigatório.");

        if (turno.Length > 50)
            throw new DomainException("O turno não pode ter mais de 50 caracteres.");
    }

    private static void ValidarAnoLetivo(int anoLetivo)
    {
        if (anoLetivo < 2000 || anoLetivo > 2100)
            throw new DomainException("O ano letivo deve estar entre 2000 e 2100.");
    }
}
