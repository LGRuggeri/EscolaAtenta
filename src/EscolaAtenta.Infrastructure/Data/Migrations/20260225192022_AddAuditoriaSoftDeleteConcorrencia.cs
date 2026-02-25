using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditoriaSoftDeleteConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamadas_TurmaId",
                table: "Chamadas");

            migrationBuilder.DropIndex(
                name: "IX_Alunos_Matricula",
                table: "Alunos");

            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_AlunoId",
                table: "AlertasEvasao");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Turmas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Intervenção manual: atualiza registros existentes para Ativo = true
            // Isso previne que o Global Query Filter oculte toda a base de dados legada
            migrationBuilder.Sql("UPDATE \"Turmas\" SET \"Ativo\" = true;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataAtualizacao",
                table: "Turmas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataCriacao",
                table: "Turmas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataExclusao",
                table: "Turmas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioAtualizacao",
                table: "Turmas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriacao",
                table: "Turmas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioExclusao",
                table: "Turmas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataAtualizacao",
                table: "RegistrosPresenca",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataCriacao",
                table: "RegistrosPresenca",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "UsuarioAtualizacao",
                table: "RegistrosPresenca",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriacao",
                table: "RegistrosPresenca",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RegistrosPresenca",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataAtualizacao",
                table: "Chamadas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataCriacao",
                table: "Chamadas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "UsuarioAtualizacao",
                table: "Chamadas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriacao",
                table: "Chamadas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Chamadas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Alunos",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Intervenção manual: atualiza registros existentes para Ativo = true
            // Isso previne que o Global Query Filter oculte toda a base de dados legada
            migrationBuilder.Sql("UPDATE \"Alunos\" SET \"Ativo\" = true;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataAtualizacao",
                table: "Alunos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataCriacao",
                table: "Alunos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataExclusao",
                table: "Alunos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioAtualizacao",
                table: "Alunos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriacao",
                table: "Alunos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioExclusao",
                table: "Alunos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataAtualizacao",
                table: "AlertasEvasao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataCriacao",
                table: "AlertasEvasao",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataResolucao",
                table: "AlertasEvasao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "AlertasEvasao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservacaoResolucao",
                table: "AlertasEvasao",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioAtualizacao",
                table: "AlertasEvasao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriacao",
                table: "AlertasEvasao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turmas_Ativo",
                table: "Turmas",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_TurmaId_DataHora",
                table: "Chamadas",
                columns: new[] { "TurmaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_Ativo",
                table: "Alunos",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_Matricula",
                table: "Alunos",
                column: "Matricula",
                unique: true,
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_AlunoId_Resolvido",
                table: "AlertasEvasao",
                columns: new[] { "AlunoId", "Resolvido" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Turmas_Ativo",
                table: "Turmas");

            migrationBuilder.DropIndex(
                name: "IX_Chamadas_TurmaId_DataHora",
                table: "Chamadas");

            migrationBuilder.DropIndex(
                name: "IX_Alunos_Ativo",
                table: "Alunos");

            migrationBuilder.DropIndex(
                name: "IX_Alunos_Matricula",
                table: "Alunos");

            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_AlunoId_Resolvido",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "DataExclusao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "UsuarioAtualizacao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "UsuarioCriacao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "UsuarioExclusao",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "UsuarioAtualizacao",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "UsuarioCriacao",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RegistrosPresenca");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "UsuarioAtualizacao",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "UsuarioCriacao",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Chamadas");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataExclusao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "UsuarioAtualizacao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "UsuarioCriacao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "UsuarioExclusao",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "DataResolucao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "ObservacaoResolucao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "UsuarioAtualizacao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "UsuarioCriacao",
                table: "AlertasEvasao");

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_TurmaId",
                table: "Chamadas",
                column: "TurmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_Matricula",
                table: "Alunos",
                column: "Matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_AlunoId",
                table: "AlertasEvasao",
                column: "AlunoId");
        }
    }
}
