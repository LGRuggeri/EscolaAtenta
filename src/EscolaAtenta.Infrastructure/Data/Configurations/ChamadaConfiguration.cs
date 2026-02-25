using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class ChamadaConfiguration : IEntityTypeConfiguration<Chamada>
{
    public void Configure(EntityTypeBuilder<Chamada> builder)
    {
        builder.ToTable("Chamadas");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.DataHora).IsRequired();
        builder.Property(c => c.ResponsavelId).IsRequired();

        // ── Concorrência Otimista via xmin do PostgreSQL ───────────────────────
        // O xmin é o transaction ID nativo do PostgreSQL — muda a cada UPDATE.
        // Configurado via Property para mapear a coluna de sistema "xmin".
        // O EF Core inclui xmin no WHERE do UPDATE e lança DbUpdateConcurrencyException
        // se outro processo modificou o registro entre o SELECT e o UPDATE.
        // Nota: UseXminAsConcurrencyToken() é um método de extensão do Npgsql
        // que internamente faz o mesmo que a configuração abaixo.
        builder.Property<uint>("xmin")
               .HasColumnName("xmin")
               .HasColumnType("xid")
               .ValueGeneratedOnAddOrUpdate()
               .IsConcurrencyToken();

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(c => c.DataCriacao).IsRequired();
        builder.Property(c => c.DataAtualizacao);
        builder.Property(c => c.UsuarioCriacao).HasMaxLength(200);
        builder.Property(c => c.UsuarioAtualizacao).HasMaxLength(200);

        // ── Backing field para coleção ─────────────────────────────────────────
        builder.Navigation(c => c.RegistrosPresenca)
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Relacionamentos ────────────────────────────────────────────────────
        builder.HasMany(c => c.RegistrosPresenca)
               .WithOne(rp => rp.Chamada)
               .HasForeignKey(rp => rp.ChamadaId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índice para busca de chamadas por turma e data
        builder.HasIndex(c => new { c.TurmaId, c.DataHora });
    }
}
