using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqliteCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdExterno = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TabelaOrigem = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SincronizadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Turmas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Turno = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AnoLetivo = table.Column<int>(type: "INTEGER", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DataExclusao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioExclusao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turmas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HashSenha = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Papel = table.Column<int>(type: "INTEGER", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DataExclusao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioExclusao = table.Column<string>(type: "TEXT", nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alunos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Matricula = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TurmaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FaltasConsecutivasAtuais = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalFaltas = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AtrasosNoTrimestre = table.Column<int>(type: "INTEGER", nullable: false),
                    FaltasNoTrimestre = table.Column<int>(type: "INTEGER", nullable: false),
                    DataInicioTrimestre = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DataExclusao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioExclusao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alunos_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chamadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DataHora = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TurmaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chamadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chamadas_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertasEvasao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlunoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TurmaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Nivel = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DataAlerta = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Resolvido = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DataResolucao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ObservacaoResolucao = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ResolvidoPorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    JustificativaResolucao = table.Column<string>(type: "TEXT", maxLength: 1500, nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasEvasao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertasEvasao_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlertasEvasao_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AlertasEvasao_Usuarios_ResolvidoPorId",
                        column: x => x.ResolvidoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosPresenca",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChamadaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlunoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosPresenca", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosPresenca_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosPresenca_Chamadas_ChamadaId",
                        column: x => x.ChamadaId,
                        principalTable: "Chamadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_AlunoId_Resolvido",
                table: "AlertasEvasao",
                columns: new[] { "AlunoId", "Resolvido" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Auditoria",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "DataResolucao", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Resolvido_DataResolucao",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "DataResolucao" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Resolvido_Tipo_Nivel",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "Tipo", "Nivel" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_ResolvidoPorId",
                table: "AlertasEvasao",
                column: "ResolvidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_TurmaId_Resolvido",
                table: "AlertasEvasao",
                columns: new[] { "TurmaId", "Resolvido" });

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_Ativo",
                table: "Alunos",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_TurmaId",
                table: "Alunos",
                column: "TurmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_TurmaId_DataHora",
                table: "Chamadas",
                columns: new[] { "TurmaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosPresenca_AlunoId",
                table: "RegistrosPresenca",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosPresenca_ChamadaId_AlunoId",
                table: "RegistrosPresenca",
                columns: new[] { "ChamadaId", "AlunoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_EntidadeId",
                table: "SyncLogs",
                column: "EntidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_IdExterno",
                table: "SyncLogs",
                column: "IdExterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turmas_Ativo",
                table: "Turmas",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email_Ativo",
                table: "Usuarios",
                columns: new[] { "Email", "Ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasEvasao");

            migrationBuilder.DropTable(
                name: "RegistrosPresenca");

            migrationBuilder.DropTable(
                name: "SyncLogs");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Alunos");

            migrationBuilder.DropTable(
                name: "Chamadas");

            migrationBuilder.DropTable(
                name: "Turmas");
        }
    }
}
