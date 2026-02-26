using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "DataAtualizacao", "DataCriacao" },
                values: new object[] { new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "DataAtualizacao", "DataCriacao" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 2, 25, 21, 25, 0, 326, DateTimeKind.Unspecified).AddTicks(7223), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 2, 25, 21, 25, 0, 326, DateTimeKind.Unspecified).AddTicks(7220), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
