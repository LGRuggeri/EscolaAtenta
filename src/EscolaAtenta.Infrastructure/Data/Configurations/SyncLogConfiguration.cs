using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> builder)
    {
        builder.ToTable("SyncLogs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.IdExterno)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(s => s.EntidadeId)
               .IsRequired();

        builder.Property(s => s.TabelaOrigem)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(s => s.SincronizadoEm)
               .IsRequired();

        // Índice único no IdExterno para garantir idempotência e busca rápida
        builder.HasIndex(s => s.IdExterno)
               .IsUnique();

        // Índice no EntidadeId para lookups durante processamento de Updates
        builder.HasIndex(s => s.EntidadeId);
    }
}
