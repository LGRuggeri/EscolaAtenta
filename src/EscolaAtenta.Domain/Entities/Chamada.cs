using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa uma chamada (registro de presença) realizada em uma turma.
/// 
/// Invariantes protegidas:
/// 1. Uma chamada deve ter uma turma e um responsável válidos.
/// 2. Um aluno não pode ter mais de um registro de presença na mesma chamada.
/// 3. Registros de presença só podem ser adicionados via RegistrarPresenca().
/// 
/// Decisão: A coleção _registrosPresenca usa List<T> internamente mas expõe
/// IReadOnlyCollection<T> para impedir mutação externa. O EF Core consegue
/// popular a coleção via backing field configurado no AppDbContext.
/// </summary>
public class Chamada : EntityBase
{
    // Backing field privado — EF Core popula via configuração HasField("_registrosPresenca")
    private readonly List<RegistroPresenca> _registrosPresenca = [];

    // Construtor privado para uso exclusivo do EF Core (materialização de queries)
    private Chamada() { }

    /// <summary>
    /// Cria uma nova chamada validando todas as invariantes.
    /// </summary>
    public Chamada(Guid id, DateTimeOffset dataHora, Guid turmaId, Guid responsavelId)
        : base(id)
    {
        if (turmaId == Guid.Empty)
            throw new DomainException("A chamada deve estar associada a uma turma válida.");

        if (responsavelId == Guid.Empty)
            throw new DomainException("A chamada deve ter um responsável válido.");

        DataHora = dataHora;
        TurmaId = turmaId;
        ResponsavelId = responsavelId;
    }

    public DateTimeOffset DataHora { get; private set; }
    public Guid TurmaId { get; private set; }
    public Guid ResponsavelId { get; private set; }

    // Propriedade de navegação — somente leitura para o mundo externo
    public virtual Turma Turma { get; private set; } = null!;

    /// <summary>
    /// Registros de presença desta chamada.
    /// Imutável externamente — use RegistrarPresenca() para adicionar.
    /// </summary>
    public IReadOnlyCollection<RegistroPresenca> RegistrosPresenca => _registrosPresenca.AsReadOnly();

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Registra a presença de um aluno nesta chamada.
    /// 
    /// Regra de negócio: Um aluno não pode ter dois registros na mesma chamada.
    /// A validação ocorre aqui no domínio, garantindo que nenhuma camada externa
    /// possa violar esta invariante.
    /// 
    /// O evento PresencaRegistradaEvent é disparado para permitir que outros
    /// componentes reajam ao registro sem acoplamento direto.
    /// </summary>
    /// <param name="alunoId">Id do aluno a ter a presença registrada.</param>
    /// <param name="status">Status de presença do aluno.</param>
    /// <returns>O RegistroPresenca criado.</returns>
    /// <exception cref="DomainException">Se o aluno já tiver registro nesta chamada.</exception>
    public RegistroPresenca RegistrarPresenca(Guid alunoId, StatusPresenca status)
    {
        if (alunoId == Guid.Empty)
            throw new DomainException("O Id do aluno não pode ser vazio.");

        // Invariante: duplicidade de aluno na mesma chamada
        if (_registrosPresenca.Any(rp => rp.AlunoId == alunoId))
            throw new DomainException(
                $"O aluno '{alunoId}' já possui registro de presença nesta chamada.");

        var registro = new RegistroPresenca(Guid.NewGuid(), Id, alunoId, status);
        _registrosPresenca.Add(registro);

        // Dispara evento para rastreabilidade e integração futura
        AddDomainEvent(new PresencaRegistradaEvent(Id, alunoId, TurmaId, status));

        return registro;
    }
}
