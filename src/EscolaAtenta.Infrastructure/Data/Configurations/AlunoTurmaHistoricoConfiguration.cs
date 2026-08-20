using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class AlunoTurmaHistoricoConfiguration : IEntityTypeConfiguration<AlunoTurmaHistorico>
{
    public void Configure(EntityTypeBuilder<AlunoTurmaHistorico> builder)
    {
        builder.ToTable("AlunosTurmasHistorico");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.AnoLetivo).IsRequired();
        builder.Property(h => h.DataInicio).IsRequired();
        builder.Property(h => h.DataFim);
        builder.Property(h => h.Motivo).HasMaxLength(500);

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(h => h.DataCriacao).IsRequired();
        builder.Property(h => h.DataAtualizacao);
        builder.Property(h => h.UsuarioCriacao).HasMaxLength(200);
        builder.Property(h => h.UsuarioAtualizacao).HasMaxLength(200);

        // ── Relacionamentos ────────────────────────────────────────────────────
        builder.HasOne(h => h.Aluno)
               .WithMany(a => a.HistoricoTurmas)
               .HasForeignKey(h => h.AlunoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Turma)
               .WithMany()
               .HasForeignKey(h => h.TurmaId)
               .OnDelete(DeleteBehavior.Restrict);

        // ── Índices ────────────────────────────────────────────────────────────
        builder.HasIndex(h => new { h.AlunoId, h.DataFim });
        builder.HasIndex(h => new { h.TurmaId, h.AnoLetivo });
    }
}
