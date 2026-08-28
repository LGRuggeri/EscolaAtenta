using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Infrastructure.Data;

/// <summary>
/// Contexto principal do banco de dados da aplicação.
/// 
/// Responsabilidades:
/// 1. Mapeamento de entidades via IEntityTypeConfiguration separadas.
/// 2. Global Query Filters para Soft Delete — entidades ISoftDeletable
///    são filtradas automaticamente (Ativo = true) em todas as queries.
/// 3. Auditoria automática no SaveChangesAsync — preenche DataCriacao,
///    DataAtualizacao, UsuarioCriacao e UsuarioAtualizacao.
/// 4. Soft Delete interceptado no SaveChangesAsync — converte Delete em Update.
/// 5. Despacho de Domain Events após a persistência bem-sucedida.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly IEscolaTenantProvider _escolaTenantProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService,
        IMediator mediator,
        IEscolaTenantProvider escolaTenantProvider)
        : base(options)
    {
        _currentUserService = currentUserService;
        _mediator = mediator;
        _escolaTenantProvider = escolaTenantProvider;
    }

    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Chamada> Chamadas => Set<Chamada>();
    public DbSet<RegistroPresenca> RegistrosPresenca => Set<RegistroPresenca>();
    public DbSet<AlertaEvasao> AlertasEvasao => Set<AlertaEvasao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UsuarioTurma> UsuarioTurmas => Set<UsuarioTurma>();
    public DbSet<AlunoTurmaHistorico> AlunosTurmasHistorico => Set<AlunoTurmaHistorico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as configurações IEntityTypeConfiguration<T> do assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Global Query Filters — Soft Delete ─────────────────────────────────
        // Decisão: Filtrar por Ativo = true em todas as queries de Aluno e Turma.
        // Para acessar registros inativos, use .IgnoreQueryFilters() explicitamente.
        // Isso garante que código que "esquece" de filtrar nunca retorne dados excluídos.
        modelBuilder.Entity<Aluno>()
                    .HasQueryFilter(a => a.Ativo);

        modelBuilder.Entity<Turma>()
                    .HasQueryFilter(t => t.Ativo);

        // Global Query Filter para Usuario - so retorna usuarios ativos
        modelBuilder.Entity<Usuario>()
                    .HasQueryFilter(u => u.Ativo);

        // NOTA: O EF Core emite warnings sobre "required end of a relationship with a filtered entity"
        // para relacionamentos como Turma->Chamada, Aluno->RegistroPresenca, Usuario->RefreshToken/UsuarioTurma.
        // Adicionar query filters correspondentes nas entidades filhas eliminaria os warnings, mas
        // mudaria o comportamento das queries de forma sutil (ex.: esconder chamadas de turmas inativas,
        // exigir que todo UsuarioTurma tenha usuario/turma no banco, etc.).
        // Optamos por manter o comportamento atual e monitorar; os warnings não causam erros de runtime.

        // A inicialização do Administrador e a senha forte são gerenciadas agora pelo DatabaseSeeder
        // durante o pipeline de startup em Program.cs para garantir senhas aleatórias e seguras.
    }

    /// <summary>
    /// Override do SaveChangesAsync para:
    /// 1. Interceptar exclusões de ISoftDeletable e convertê-las em updates.
    /// 2. Preencher campos de auditoria automaticamente.
    /// 3. Despachar Domain Events após a persistência bem-sucedida.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var agora = DateTimeOffset.UtcNow;
        var usuarioAtual = _currentUserService.UsuarioId;

        // ── Proteção contra Hard Deletes e Sincronização em Nuvem (Multi-Tenant) ──
        
        var deletedEntities = ChangeTracker.Entries<EntityBase>().Where(e => e.State == EntityState.Deleted).ToList();
        foreach (var entry in deletedEntities)
        {
            if (entry.Entity is not ISoftDeletable)
            {
                throw new InvalidOperationException($"Não é permitido excluir fisicamente a entidade {entry.Entity.GetType().Name} porque ela é sincronizável com a Nuvem. Implemente ISoftDeletable para exclusão lógica.");
            }
            
            // É ISoftDeletable, converte Delete para Modified (Soft Delete)
            entry.State = EntityState.Modified;
            entry.CurrentValues[nameof(ISoftDeletable.Ativo)] = false;
            entry.CurrentValues[nameof(ISoftDeletable.DataExclusao)] = agora;
            entry.CurrentValues[nameof(ISoftDeletable.UsuarioExclusao)] = usuarioAtual;
            
            // Força o reenvio para a Nuvem
            entry.CurrentValues[nameof(EntityBase.CloudSyncedAt)] = null;
        }

        // ── Preenchimento de Auditoria e Multi-Tenant ─────────────────────────
        // Usamos entry.CurrentValues para contornar a restrição de acesso
        // das propriedades internal set do EntityBase (assembly diferente).
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            // Pula as entidades que acabaram de sofrer soft-delete acima para não sobresscrever auditoria duplicada, 
            // mas processa-as no switch Modified, exceto se tomarmos cuidado (EntityState.Modified foi setado acima).
            
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.CurrentValues[nameof(EntityBase.DataCriacao)] = agora;
                    entry.CurrentValues[nameof(EntityBase.UsuarioCriacao)] = usuarioAtual;
                    
                    // Preenche automaticamente o identificador do App Local (Edge Node)
                    entry.CurrentValues[nameof(EntityBase.EscolaId)] = _escolaTenantProvider.EscolaId;
                    entry.CurrentValues[nameof(EntityBase.CloudSyncedAt)] = null;
                    break;

                case EntityState.Modified:
                    entry.CurrentValues[nameof(EntityBase.DataAtualizacao)] = agora;
                    entry.CurrentValues[nameof(EntityBase.UsuarioAtualizacao)] = usuarioAtual;

                    // Modificou localmente, precisa enviar o delta para a Nuvem
                    entry.CurrentValues[nameof(EntityBase.CloudSyncedAt)] = null;

                    // Protege campos de criação contra sobrescrita acidental
                    entry.Property(e => e.DataCriacao).IsModified = false;
                    entry.Property(e => e.UsuarioCriacao).IsModified = false;

                    // Protege EscolaId (o dono nunca muda)
                    entry.Property(e => e.EscolaId).IsModified = false;

                    // Nota: não protegemos Ativo/DataExclusao/UsuarioExclusao aqui
                    // porque a reativação e o soft delete são operações de domínio
                    // legítimas realizadas pelos métodos Reativar/Desativar.
                    // A segurança dessas mudanças fica nos handlers autorizados.
                    break;
            }
        }

        // ── Coleta de Domain Events ────────────────────────────────────────────────
        // Coletamos e limpamos os eventos ANTES do commit, mas publicamos APÓS o
        // SaveChanges. Isso garante que os handlers leiam o estado persistido,
        // evitando duplicatas e decisões em cima de dados ainda não commitados.
        var domainEvents = ColetarEDedupDomainEvents();

        // ── Persistência Atômica ───────────────────────────────────────────────────────
        var resultado = await base.SaveChangesAsync(cancellationToken);

        // ── Despacho de Domain Events após commit ────────────────────────────────
        // Se handlers criarem novas entidades, salvamos em iterações subsequentes
        // com um limite máximo para evitar loops infinitos.
        if (domainEvents.Count > 0)
        {
            const int maxIteracoes = 5;
            for (int i = 0; i < maxIteracoes; i++)
            {
                foreach (var domainEvent in domainEvents)
                {
                    await _mediator.Publish(domainEvent, cancellationToken);
                }

                // Os handlers podem ter criado/modificado entidades (ex: AlertaEvasao)
                // sem gerar novos Domain Events. Persistimos qualquer mudança pendente.
                if (!ChangeTracker.HasChanges())
                {
                    var eventosCascata = ColetarEDedupDomainEvents();
                    if (eventosCascata.Count == 0)
                        break;

                    domainEvents = eventosCascata;
                    continue;
                }

                await base.SaveChangesAsync(cancellationToken);

                // Coleta novos eventos gerados durante o SaveChanges cascata.
                domainEvents = ColetarEDedupDomainEvents();
                if (domainEvents.Count == 0)
                    break;
            }
        }

        return resultado;
    }

    private List<INotification> ColetarEDedupDomainEvents()
    {
        var entidadesComEventos = ChangeTracker
            .Entries<EntityBase>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (!entidadesComEventos.Any())
            return [];

        var domainEvents = entidadesComEventos
            .SelectMany(e => e.DomainEvents)
            .ToList();

        entidadesComEventos.ForEach(e => e.ClearDomainEvents());

        // ── Deduplicação defensiva de eventos de threshold ─────────────────────
        // Mantemos apenas o último evento de threshold por (AlunoId, Tipo)
        // para cada batch, evitando alertas duplicados.
        var eventosVistos = new HashSet<string>();
        var eventosFiltrados = new List<INotification>();
        for (int i = domainEvents.Count - 1; i >= 0; i--)
        {
            var evt = domainEvents[i];
            var chave = evt switch
            {
                LimiteFaltasAtingidoEvent e => $"{e.AlunoId}:{nameof(LimiteFaltasAtingidoEvent)}",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(chave))
            {
                eventosFiltrados.Insert(0, evt);
                continue;
            }

            if (!eventosVistos.Contains(chave))
            {
                eventosVistos.Add(chave);
                eventosFiltrados.Insert(0, evt);
            }
        }

        return eventosFiltrados;
    }
}
