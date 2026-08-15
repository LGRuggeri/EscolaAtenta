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

            // Deduplica chamadas históricas do mesmo dia/turma antes de adicionar o índice único.
            // Mantém a chamada mais recente (por DataCriacao, desempate por Id).
            migrationBuilder.Sql(@"
                DELETE FROM RegistrosPresenca
                WHERE ChamadaId IN (
                    SELECT c.Id FROM Chamadas c
                    WHERE EXISTS (
                        SELECT 1 FROM Chamadas c2
                        WHERE c2.TurmaId = c.TurmaId
                          AND c2.DataChamada = c.DataChamada
                          AND (c2.DataCriacao > c.DataCriacao OR (c2.DataCriacao = c.DataCriacao AND c2.Id > c.Id))
                    )
                );

                DELETE FROM Chamadas
                WHERE Id IN (
                    SELECT c.Id FROM Chamadas c
                    WHERE EXISTS (
                        SELECT 1 FROM Chamadas c2
                        WHERE c2.TurmaId = c.TurmaId
                          AND c2.DataChamada = c.DataChamada
                          AND (c2.DataCriacao > c.DataCriacao OR (c2.DataCriacao = c.DataCriacao AND c2.Id > c.Id))
                    )
                );");

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
