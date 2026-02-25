using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração do mapeamento EF Core para a entidade Turma.
/// 
/// Decisão: Separar configurações em classes IEntityTypeConfiguration<T>
/// mantém o AppDbContext limpo e cada configuração focada em uma entidade.
/// </summary>
public class TurmaConfiguration : IEntityTypeConfiguration<Turma>
{
    public void Configure(EntityTypeBuilder<Turma> builder)
    {
        builder.ToTable("Turmas");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(t => t.Turno)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(t => t.AnoLetivo)
               .IsRequired();

        // ── Soft Delete ────────────────────────────────────────────────────────
        builder.Property(t => t.Ativo)
               .IsRequired()
               .HasDefaultValue(true);

        builder.Property(t => t.DataExclusao);
        builder.Property(t => t.UsuarioExclusao).HasMaxLength(200);

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(t => t.DataCriacao).IsRequired();
        builder.Property(t => t.DataAtualizacao);
        builder.Property(t => t.UsuarioCriacao).HasMaxLength(200);
        builder.Property(t => t.UsuarioAtualizacao).HasMaxLength(200);

        // ── Relacionamentos ────────────────────────────────────────────────────
        // Backing fields para coleções — EF Core popula via reflexão
        builder.HasMany(t => t.Alunos)
               .WithOne(a => a.Turma)
               .HasForeignKey(a => a.TurmaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Chamadas)
               .WithOne(c => c.Turma)
               .HasForeignKey(c => c.TurmaId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índice para performance em queries de turmas ativas
        builder.HasIndex(t => t.Ativo);
    }
}
