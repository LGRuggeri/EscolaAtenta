using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantCloudSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "Usuarios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "Usuarios",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "Turmas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "Turmas",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "RegistrosPresenca",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "RegistrosPresenca",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "Chamadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "Chamadas",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "Alunos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "Alunos",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudSyncedAt",
                table: "AlertasEvasao",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolaId",
                table: "AlertasEvasao",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "CloudSyncedAt",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "EscolaId",
                table: "AlertasEvasao");
        }
    }
}
