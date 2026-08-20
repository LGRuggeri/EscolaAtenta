using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaHistoricoMatriculasEConfiguracaoEscolar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlunosTurmasHistorico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlunoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TurmaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnoLetivo = table.Column<int>(type: "INTEGER", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataFim = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EscolaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloudSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlunosTurmasHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlunosTurmasHistorico_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlunosTurmasHistorico_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesEscola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TipoPeriodoLetivo = table.Column<int>(type: "INTEGER", nullable: false),
                    EscolaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloudSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesEscola", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlunosTurmasHistorico_AlunoId_DataFim",
                table: "AlunosTurmasHistorico",
                columns: new[] { "AlunoId", "DataFim" });

            migrationBuilder.CreateIndex(
                name: "IX_AlunosTurmasHistorico_TurmaId_AnoLetivo",
                table: "AlunosTurmasHistorico",
                columns: new[] { "TurmaId", "AnoLetivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlunosTurmasHistorico");

            migrationBuilder.DropTable(
                name: "ConfiguracoesEscola");
        }
    }
}
