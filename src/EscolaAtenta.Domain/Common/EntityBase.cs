namespace EscolaAtenta.Domain.Common;

/// <summary>
/// Classe base para todas as entidades do domínio.
/// 
/// Responsabilidades:
/// 1. Identidade: Garante que toda entidade tenha um Id do tipo Guid.
/// 2. Auditoria: Campos DataCriacao, DataAtualizacao, UsuarioCriacao e UsuarioAtualizacao
///    são preenchidos automaticamente pelo AppDbContext no SaveChangesAsync.
/// 3. Domain Events: Gerencia a coleção de eventos de domínio pendentes de despacho.
///    Os eventos são coletados durante a execução de métodos de negócio e despachados
///    APÓS o SaveChangesAsync para garantir consistência transacional.
/// 
/// Decisão sobre Guid: Usamos Guid gerado no lado do cliente (domínio) em vez de
/// identity do banco. Isso permite criar entidades sem round-trip ao banco e facilita
/// testes unitários sem dependência de infraestrutura.
/// </summary>
public abstract class EntityBase
{
    // Backing field privado para a coleção de eventos — impede acesso externo direto
    private readonly List<IDomainEvent> _domainEvents = [];

    protected EntityBase() { }

    protected EntityBase(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("O Id da entidade não pode ser um Guid vazio.", nameof(id));

        Id = id;
    }

    public Guid Id { get; private set; }

    // ── Preparação Multi-Tenant e Cloud Sync ───────────────────────────────────
    
    /// <summary>Identificador da Escola (Tenant) ao qual este registro pertence.</summary>
    public Guid EscolaId { get; internal set; }

    /// <summary>Data e hora em que este registro foi sincronizado com a Nuvem. Null se houver alterações locais pendentes de envio.</summary>
    public DateTime? CloudSyncedAt { get; internal set; }

    // ── Campos de Auditoria ────────────────────────────────────────────────────
    // Preenchidos automaticamente pelo AppDbContext.SaveChangesAsync

    /// <summary>Data e hora de criação do registro, sempre em UTC.</summary>
    public DateTimeOffset DataCriacao { get; internal set; }

    /// <summary>Data e hora da última atualização, null se nunca atualizado.</summary>
    public DateTimeOffset? DataAtualizacao { get; internal set; }

    /// <summary>Identificador do usuário que criou o registro.</summary>
    public string? UsuarioCriacao { get; internal set; }

    /// <summary>Identificador do usuário que realizou a última atualização.</summary>
    public string? UsuarioAtualizacao { get; internal set; }

    // ── Domain Events ──────────────────────────────────────────────────────────

    /// <summary>
    /// Coleção somente-leitura de eventos de domínio pendentes.
    /// O AppDbContext lê esta coleção após SaveChangesAsync para despachar via MediatR.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Registra um evento de domínio para ser despachado após a persistência.
    /// Chamado exclusivamente por métodos de negócio dentro da própria entidade.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Remove todos os eventos pendentes após o despacho.
    /// Chamado pelo DomainEventDispatcher após processar os eventos.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
