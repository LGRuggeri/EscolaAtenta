using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class AlertaEvasaoConfiguration : IEntityTypeConfiguration<AlertaEvasao>
{
    public void Configure(EntityTypeBuilder<AlertaEvasao> builder)
    {
        builder.ToTable("AlertasEvasao");

        builder.HasKey(ae => ae.Id);

        builder.Property(ae => ae.DataAlerta).IsRequired();

        builder.Property(ae => ae.Descricao)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(ae => ae.Resolvido)
               .IsRequired()
               .HasDefaultValue(false);

        builder.Property(ae => ae.DataResolucao);

        builder.Property(ae => ae.ObservacaoResolucao)
               .HasMaxLength(1000);

        builder.Property(ae => ae.JustificativaResolucao)
               .HasMaxLength(1500);

        builder.HasOne(ae => ae.ResolvidoPor)
               .WithMany()
               .HasForeignKey(ae => ae.ResolvidoPorId)
               .OnDelete(DeleteBehavior.Restrict);

        // ── Auditoria ──────────────────────────────────────────────────────────
        builder.Property(ae => ae.DataCriacao).IsRequired();
        builder.Property(ae => ae.DataAtualizacao);
        builder.Property(ae => ae.UsuarioCriacao).HasMaxLength(200);
        builder.Property(ae => ae.UsuarioAtualizacao).HasMaxLength(200);

        builder.Property(ae => ae.AlunoId).IsRequired(false);
        builder.Property(ae => ae.TurmaId).IsRequired(false);

        // ── TipoAlerta ──────────────────────────────────────────────────────
        // HasConversion<int>(): persiste como integer no banco (Evasao=1, Atraso=2)
        // HasDefaultValue(TipoAlerta.Evasao): todos os registros pré-existentes
        // (criados antes desta migration) receberão automáticamente o valor 1.
        builder.Property(ae => ae.Tipo)
               .IsRequired()
               .HasDefaultValue(TipoAlerta.Evasao)
               .HasConversion<int>();

        // Índice para busca de alertas não resolvidos por aluno ou turma
        builder.HasIndex(ae => new { ae.AlunoId, ae.Resolvido });
        builder.HasIndex(ae => new { ae.TurmaId, ae.Resolvido });
        
        // Índice composto para busca no dashboard mitigando gargalo
        builder.HasIndex(a => new { a.Resolvido, a.Tipo, a.Nivel });

        builder.HasIndex(a => new { a.Resolvido, a.DataResolucao });

        // Índice composto dedicado à query de Auditoria de Alertas.
        // Cobre o filtro WHERE Resolvido=true, ORDER BY DataResolucao DESC e o filtro Tipo
        // sem Full Table Scan conforme a tabela cresce em produção.
        builder.HasIndex(a => new { a.Resolvido, a.DataResolucao, a.Tipo })
               .HasDatabaseName("IX_AlertasEvasao_Auditoria");
    }
}
