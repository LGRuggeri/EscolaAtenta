using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIndexToAlertasEvasao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Auditoria",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "DataResolucao", "Tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_Auditoria",
                table: "AlertasEvasao");
        }
    }
}
