using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddControleFaltasEvasao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FaltasConsecutivasAtuais",
                table: "Alunos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalFaltas",
                table: "Alunos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HashSenha = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Papel = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DataExclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsuarioExclusao = table.Column<string>(type: "text", nullable: true),
                    DataCriacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsuarioCriacao = table.Column<string>(type: "text", nullable: true),
                    UsuarioAtualizacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Ativo", "DataAtualizacao", "DataCriacao", "DataExclusao", "Email", "HashSenha", "Papel", "UsuarioAtualizacao", "UsuarioCriacao", "UsuarioExclusao" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 2, 25, 21, 25, 0, 326, DateTimeKind.Unspecified).AddTicks(7223), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 2, 25, 21, 25, 0, 326, DateTimeKind.Unspecified).AddTicks(7220), new TimeSpan(0, 0, 0, 0, 0)), null, "admin@escolaatenta.com.br", "$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", 3, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true,
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email_Ativo",
                table: "Usuarios",
                columns: new[] { "Email", "Ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FaltasConsecutivasAtuais",
                table: "Alunos");

            migrationBuilder.DropColumn(
                name: "TotalFaltas",
                table: "Alunos");
        }
    }
}
