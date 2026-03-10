using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class RegistroPresencaConfiguration : IEntityTypeConfiguration<RegistroPresenca>
{
    public void Configure(EntityTypeBuilder<RegistroPresenca> builder)
    {
        builder.ToTable("RegistrosPresenca");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Status)
               .IsRequired()
               .HasConversion<int>();

        // ── Concorrência Otimista ─────────────────────────────────────────────
        // SQLite: concorrência otimista não é necessária em ambiente escolar
        // mono-usuário. Se futuramente for necessário, usar uma coluna
        // RowVersion (byte[]) com ValueGeneratedOnAddOrUpdate().

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(rp => rp.DataCriacao).IsRequired();
        builder.Property(rp => rp.DataAtualizacao);
        builder.Property(rp => rp.UsuarioCriacao).HasMaxLength(200);
        builder.Property(rp => rp.UsuarioAtualizacao).HasMaxLength(200);

        // ── Índice único composto ──────────────────────────────────────────────
        // Garante no banco que um aluno não pode ter dois registros na mesma chamada.
        // Esta é a segunda linha de defesa — a primeira é a validação no domínio.
        builder.HasIndex(rp => new { rp.ChamadaId, rp.AlunoId })
               .IsUnique();
    }
}
