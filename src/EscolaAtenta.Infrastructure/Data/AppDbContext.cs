using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Chamada> Chamadas => Set<Chamada>();
    public DbSet<RegistroPresenca> RegistrosPresenca => Set<RegistroPresenca>();
    public DbSet<AlertaEvasao> AlertasEvasao => Set<AlertaEvasao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ──────────────────────────────────────
        // Turma
        // ──────────────────────────────────────
        modelBuilder.Entity<Turma>(entity =>
        {
            entity.ToTable("Turmas");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Nome)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(t => t.Turno)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(t => t.AnoLetivo)
                  .IsRequired();

            entity.HasMany(t => t.Alunos)
                  .WithOne(a => a.Turma)
                  .HasForeignKey(a => a.TurmaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(t => t.Chamadas)
                  .WithOne(c => c.Turma)
                  .HasForeignKey(c => c.TurmaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ──────────────────────────────────────
        // Aluno
        // ──────────────────────────────────────
        modelBuilder.Entity<Aluno>(entity =>
        {
            entity.ToTable("Alunos");

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Nome)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(a => a.Matricula)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.HasIndex(a => a.Matricula)
                  .IsUnique();

            entity.HasMany(a => a.RegistrosPresenca)
                  .WithOne(rp => rp.Aluno)
                  .HasForeignKey(rp => rp.AlunoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(a => a.AlertasEvasao)
                  .WithOne(ae => ae.Aluno)
                  .HasForeignKey(ae => ae.AlunoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ──────────────────────────────────────
        // Chamada
        // ──────────────────────────────────────
        modelBuilder.Entity<Chamada>(entity =>
        {
            entity.ToTable("Chamadas");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.DataHora)
                  .IsRequired();

            entity.Property(c => c.ResponsavelId)
                  .IsRequired();

            entity.HasMany(c => c.RegistrosPresenca)
                  .WithOne(rp => rp.Chamada)
                  .HasForeignKey(rp => rp.ChamadaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ──────────────────────────────────────
        // RegistroPresenca
        // ──────────────────────────────────────
        modelBuilder.Entity<RegistroPresenca>(entity =>
        {
            entity.ToTable("RegistrosPresenca");

            entity.HasKey(rp => rp.Id);

            entity.Property(rp => rp.Status)
                  .IsRequired()
                  .HasConversion<int>();

            // Índice composto para evitar duplicatas: um aluno por chamada
            entity.HasIndex(rp => new { rp.ChamadaId, rp.AlunoId })
                  .IsUnique();
        });

        // ──────────────────────────────────────
        // AlertaEvasao
        // ──────────────────────────────────────
        modelBuilder.Entity<AlertaEvasao>(entity =>
        {
            entity.ToTable("AlertasEvasao");

            entity.HasKey(ae => ae.Id);

            entity.Property(ae => ae.DataAlerta)
                  .IsRequired();

            entity.Property(ae => ae.Resolvido)
                  .IsRequired()
                  .HasDefaultValue(false);
        });
    }
}
