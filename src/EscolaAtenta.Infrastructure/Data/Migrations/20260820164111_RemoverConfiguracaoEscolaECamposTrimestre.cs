using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoverConfiguracaoEscolaECamposTrimestre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesEscola");

            migrationBuilder.DropColumn(
                name: "AtrasosNoTrimestre",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataInicioTrimestre",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "FaltasNoTrimestre",
                table: "Alunos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtrasosNoTrimestre",
                table: "Alunos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicioTrimestre",
                table: "Alunos",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FaltasNoTrimestre",
                table: "Alunos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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
