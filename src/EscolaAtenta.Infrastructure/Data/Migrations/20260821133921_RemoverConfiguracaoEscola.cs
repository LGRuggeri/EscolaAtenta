using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoverConfiguracaoEscola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A tabela pode não existir em bancos que nunca receberam a migration
            // intermediária; DROP TABLE incondicional falharia na atualização do
            // servidor em produção.
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ConfiguracoesEscola\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesEscola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloudSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EscolaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TipoPeriodoLetivo = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesEscola", x => x.Id);
                });
        }
    }
}
