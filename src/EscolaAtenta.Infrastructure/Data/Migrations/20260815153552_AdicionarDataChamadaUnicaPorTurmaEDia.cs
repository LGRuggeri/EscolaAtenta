using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDataChamadaUnicaPorTurmaEDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataChamada",
                table: "Chamadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE Chamadas SET DataChamada = date(DataHora) WHERE DataChamada IS NULL");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DataChamada",
                table: "Chamadas",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_TurmaId_DataChamada",
                table: "Chamadas",
                columns: new[] { "TurmaId", "DataChamada" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamadas_TurmaId_DataChamada",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "DataChamada",
                table: "Chamadas");
        }
    }
}
