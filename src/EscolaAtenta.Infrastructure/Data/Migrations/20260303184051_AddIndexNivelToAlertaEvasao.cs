using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexNivelToAlertaEvasao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Resolvido_Tipo_Nivel",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "Tipo", "Nivel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_Resolvido_Tipo_Nivel",
                table: "AlertasEvasao");
        }
    }
}
