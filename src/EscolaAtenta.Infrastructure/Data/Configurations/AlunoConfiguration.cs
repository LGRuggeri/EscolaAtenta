using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class AlunoConfiguration : IEntityTypeConfiguration<Aluno>
{
    public void Configure(EntityTypeBuilder<Aluno> builder)
    {
        builder.ToTable("Alunos");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nome)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(a => a.Matricula)
               .IsRequired(false)
               .HasMaxLength(50);

        // ── Soft Delete ────────────────────────────────────────────────────────
        builder.Property(a => a.Ativo)
               .IsRequired()
               .HasDefaultValue(true);

        builder.Property(a => a.DataExclusao);
        builder.Property(a => a.UsuarioExclusao).HasMaxLength(200);

        // ── Controle de Faltas (Novas propriedades) ───────────────────────────────
        builder.Property(a => a.FaltasConsecutivasAtuais)
               .IsRequired()
               .HasDefaultValue(0);

        builder.Property(a => a.TotalFaltas)
               .IsRequired()
               .HasDefaultValue(0);

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(a => a.DataCriacao).IsRequired();
        builder.Property(a => a.DataAtualizacao);
        builder.Property(a => a.UsuarioCriacao).HasMaxLength(200);
        builder.Property(a => a.UsuarioAtualizacao).HasMaxLength(200);

        // ── Backing fields para coleções ───────────────────────────────────────
        // Necessário para que o EF Core popule as listas privadas via reflexão
        builder.Navigation(a => a.RegistrosPresenca)
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(a => a.AlertasEvasao)
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Relacionamentos ────────────────────────────────────────────────────
        builder.HasMany(a => a.RegistrosPresenca)
               .WithOne(rp => rp.Aluno)
               .HasForeignKey(rp => rp.AlunoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.AlertasEvasao)
               .WithOne(ae => ae.Aluno)
               .HasForeignKey(ae => ae.AlunoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Ativo);
    }
}
