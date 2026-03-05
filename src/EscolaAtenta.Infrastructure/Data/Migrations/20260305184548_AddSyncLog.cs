using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdExterno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TabelaOrigem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SincronizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_EntidadeId",
                table: "SyncLogs",
                column: "EntidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_IdExterno",
                table: "SyncLogs",
                column: "IdExterno",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncLogs");
        }
    }
}
