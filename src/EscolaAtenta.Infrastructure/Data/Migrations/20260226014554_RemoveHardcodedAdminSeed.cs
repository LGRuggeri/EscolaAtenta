using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHardcodedAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "DataAtualizacao", "DataCriacao", "DataExclusao", "Email", "HashSenha", "Papel", "UsuarioAtualizacao", "UsuarioCriacao", "UsuarioExclusao" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "admin@escolaatenta.com.br", "$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", 3, null, null, null });
        }
    }
}
