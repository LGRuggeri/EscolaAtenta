// Configuracao do EntityTypeConfiguration para Usuario
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaAtenta.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        // PK
        builder.HasKey(u => u.Id);

        // Nome
        builder.Property(u => u.Nome)
               .IsRequired()
               .HasMaxLength(100);

        // Email unico e indexado
        builder.HasIndex(u => u.Email)
               .IsUnique()
               .HasFilter("[Ativo] = 1"); // Unique parcial - apenas ativos (SQLite usa 1/0 para bool)

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(200);

        // Hash BCrypt - 60 caracteres
        builder.Property(u => u.HashSenha)
               .IsRequired()
               .HasMaxLength(60);

        // Papel (enum)
        builder.Property(u => u.Papel)
               .IsRequired()
               .HasConversion<int>(); // Armazena como int no banco

        // Ativo (Soft Delete)
        builder.Property(u => u.Ativo)
               .IsRequired()
               .HasDefaultValue(true);

        // Index para busca rapida por email + ativo
        builder.HasIndex(u => new { u.Email, u.Ativo });
    }
}
