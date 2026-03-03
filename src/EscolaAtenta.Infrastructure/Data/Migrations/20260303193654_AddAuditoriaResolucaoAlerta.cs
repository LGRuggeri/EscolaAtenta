using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaAtenta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditoriaResolucaoAlerta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JustificativaResolucao",
                table: "AlertasEvasao",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvidoPorId",
                table: "AlertasEvasao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_Resolvido_DataResolucao",
                table: "AlertasEvasao",
                columns: new[] { "Resolvido", "DataResolucao" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasEvasao_ResolvidoPorId",
                table: "AlertasEvasao",
                column: "ResolvidoPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertasEvasao_Usuarios_ResolvidoPorId",
                table: "AlertasEvasao",
                column: "ResolvidoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertasEvasao_Usuarios_ResolvidoPorId",
                table: "AlertasEvasao");

            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_Resolvido_DataResolucao",
                table: "AlertasEvasao");

            migrationBuilder.DropIndex(
                name: "IX_AlertasEvasao_ResolvidoPorId",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "JustificativaResolucao",
                table: "AlertasEvasao");

            migrationBuilder.DropColumn(
                name: "ResolvidoPorId",
                table: "AlertasEvasao");
        }
    }
}
