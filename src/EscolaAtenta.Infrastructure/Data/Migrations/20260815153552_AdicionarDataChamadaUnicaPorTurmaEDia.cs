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
            // Antes da deduplicação, guarda os alunos afetados para recalcular os contadores denormalizados.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE AlunosAfetados AS
                SELECT DISTINCT rp.AlunoId
                FROM RegistrosPresenca rp
                WHERE rp.ChamadaId IN (
                    SELECT c.Id FROM Chamadas c
                    WHERE EXISTS (
                        SELECT 1 FROM Chamadas c2
                        WHERE c2.TurmaId = c.TurmaId
                          AND c2.DataChamada = c.DataChamada
                          AND (c2.DataCriacao > c.DataCriacao OR (c2.DataCriacao = c.DataCriacao AND c2.Id > c.Id))
                    )
                );

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
                );

                -- Recalcula os contadores denormalizados dos alunos afetados pela deduplicação.
                -- TotalFaltas inclui Falta(1), Ausente(3) e FaltaJustificada(2).
                UPDATE Alunos
                SET TotalFaltas = COALESCE((
                    SELECT COUNT(*)
                    FROM RegistrosPresenca rp
                    JOIN Chamadas c ON c.Id = rp.ChamadaId
                    WHERE rp.AlunoId = Alunos.Id
                      AND rp.Status IN (1, 2, 3)
                ), 0),
                FaltasNoTrimestre = COALESCE((
                    SELECT COUNT(*)
                    FROM RegistrosPresenca rp
                    JOIN Chamadas c ON c.Id = rp.ChamadaId
                    WHERE rp.AlunoId = Alunos.Id
                      AND rp.Status IN (1, 3)
                      AND c.DataHora >= Alunos.DataInicioTrimestre
                ), 0),
                AtrasosNoTrimestre = COALESCE((
                    SELECT COUNT(*)
                    FROM RegistrosPresenca rp
                    JOIN Chamadas c ON c.Id = rp.ChamadaId
                    WHERE rp.AlunoId = Alunos.Id
                      AND rp.Status = 4
                      AND c.DataHora >= Alunos.DataInicioTrimestre
                ), 0)
                WHERE Id IN (SELECT AlunoId FROM AlunosAfetados);

                -- Recalcula faltas consecutivas atuais (apenas Falta=1 e Ausente=3).
                -- A sequência é quebrada por Presente=0 ou FaltaJustificada=2.
                UPDATE Alunos
                SET FaltasConsecutivasAtuais = COALESCE((
                    SELECT COUNT(*)
                    FROM RegistrosPresenca rp
                    JOIN Chamadas c ON c.Id = rp.ChamadaId
                    WHERE rp.AlunoId = Alunos.Id
                      AND rp.Status IN (1, 3)
                      AND c.DataHora > (
                          SELECT COALESCE(MAX(c2.DataHora), '0001-01-01')
                          FROM RegistrosPresenca rp2
                          JOIN Chamadas c2 ON c2.Id = rp2.ChamadaId
                          WHERE rp2.AlunoId = Alunos.Id
                            AND rp2.Status IN (0, 2)
                      )
                ), 0)
                WHERE Id IN (SELECT AlunoId FROM AlunosAfetados);

                DROP TABLE AlunosAfetados;");

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
