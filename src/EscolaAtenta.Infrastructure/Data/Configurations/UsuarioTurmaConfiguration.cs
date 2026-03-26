using EscolaAtenta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class UsuarioTurmaConfiguration : IEntityTypeConfiguration<UsuarioTurma>
{
    public void Configure(EntityTypeBuilder<UsuarioTurma> builder)
    {
        builder.ToTable("UsuarioTurmas");

        builder.HasKey(ut => ut.Id);

        builder.Property(ut => ut.UsuarioId).IsRequired();
        builder.Property(ut => ut.TurmaId).IsRequired();

        // Índice único: um usuário só pode ser vinculado a uma turma uma vez
        builder.HasIndex(ut => new { ut.UsuarioId, ut.TurmaId }).IsUnique();

        // Índice para busca por turma (usado nas validações IDOR)
        builder.HasIndex(ut => ut.TurmaId);

        // Auditoria
        builder.Property(ut => ut.DataCriacao).IsRequired();
        builder.Property(ut => ut.DataAtualizacao);
        builder.Property(ut => ut.UsuarioCriacao).HasMaxLength(200);
        builder.Property(ut => ut.UsuarioAtualizacao).HasMaxLength(200);

        // Relacionamentos
        builder.HasOne(ut => ut.Usuario)
               .WithMany()
               .HasForeignKey(ut => ut.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ut => ut.Turma)
               .WithMany()
               .HasForeignKey(ut => ut.TurmaId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
