using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class ConfiguracaoEscolaConfiguration : IEntityTypeConfiguration<ConfiguracaoEscola>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoEscola> builder)
    {
        builder.ToTable("ConfiguracoesEscola");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TipoPeriodoLetivo)
               .IsRequired()
               .HasConversion<int>();

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(c => c.DataCriacao).IsRequired();
        builder.Property(c => c.DataAtualizacao);
        builder.Property(c => c.UsuarioCriacao).HasMaxLength(200);
        builder.Property(c => c.UsuarioAtualizacao).HasMaxLength(200);
    }
}
