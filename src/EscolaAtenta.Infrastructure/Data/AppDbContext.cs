using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Entities;
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

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService,
        IMediator mediator)
        : base(options)
    {
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Chamada> Chamadas => Set<Chamada>();
    public DbSet<RegistroPresenca> RegistrosPresenca => Set<RegistroPresenca>();
    public DbSet<AlertaEvasao> AlertasEvasao => Set<AlertaEvasao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

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

        // ── Interceptação de Soft Delete ───────────────────────────────────────
        // Entidades marcadas para Delete que implementam ISoftDeletable
        // são convertidas para Modified com Ativo = false
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.CurrentValues[nameof(ISoftDeletable.Ativo)] = false;
            entry.CurrentValues[nameof(ISoftDeletable.DataExclusao)] = agora;
            entry.CurrentValues[nameof(ISoftDeletable.UsuarioExclusao)] = usuarioAtual;
        }

        // ── Preenchimento de Auditoria ─────────────────────────────────────────
        // Usamos entry.CurrentValues para contornar a restrição de acesso
        // das propriedades internal set do EntityBase (assembly diferente).
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.CurrentValues[nameof(EntityBase.DataCriacao)] = agora;
                    entry.CurrentValues[nameof(EntityBase.UsuarioCriacao)] = usuarioAtual;
                    break;

                case EntityState.Modified:
                    entry.CurrentValues[nameof(EntityBase.DataAtualizacao)] = agora;
                    entry.CurrentValues[nameof(EntityBase.UsuarioAtualizacao)] = usuarioAtual;
                    // Protege campos de criação contra sobrescrita acidental
                    entry.Property(e => e.DataCriacao).IsModified = false;
                    entry.Property(e => e.UsuarioCriacao).IsModified = false;
                    break;
            }
        }

        // ── Coleta de Domain Events e Despacho Atômico ────────────────────────────
        // Despachamos os eventos ANTES do commit, dentro do fluxo da mesma requisição.
        // Se os handlers alterarem entidades ou adicionarem novas, elas são
        // processadas na mesma transação banco assegurada pelo SaveChangesAsync.
        while (true)
        {
            var entidadesComEventos = ChangeTracker
                .Entries<EntityBase>()
                .Where(e => e.Entity.DomainEvents.Count != 0)
                .Select(e => e.Entity)
                .ToList();

            if (!entidadesComEventos.Any())
                break;

            var domainEvents = entidadesComEventos
                .SelectMany(e => e.DomainEvents)
                .ToList();

            entidadesComEventos.ForEach(e => e.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        // ── Persistência Atômica ───────────────────────────────────────────────────────
        var resultado = await base.SaveChangesAsync(cancellationToken);

        return resultado;
    }
}
