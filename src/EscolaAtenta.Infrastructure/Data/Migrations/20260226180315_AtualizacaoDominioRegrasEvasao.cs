using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtualizacaoDominioRegrasEvasao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtrasosNoTrimestre",
                table: "Alunos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicioTrimestre",
                table: "Alunos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FaltasNoTrimestre",
                table: "Alunos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "AlunoId",
                table: "AlertasEvasao",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "AlertasEvasao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TurmaId",
                table: "AlertasEvasao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_TurmaId_Resolvido",
                table: "AlertasEvasao",
                columns: new[] { "TurmaId", "Resolvido" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_TurmaId_Resolvido",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "AtrasosNoTrimestre",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataInicioTrimestre",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "FaltasNoTrimestre",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "TurmaId",
                table: "AlertasEvasao");

            migrationBuilder.AlterColumn<Guid>(
                name: "AlunoId",
                table: "AlertasEvasao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
