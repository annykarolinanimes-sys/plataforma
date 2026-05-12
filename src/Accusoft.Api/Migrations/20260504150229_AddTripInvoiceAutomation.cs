using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTripInvoiceAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cliente_id",
                table: "gestao_viagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destino",
                table: "gestao_viagens",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origem",
                table: "gestao_viagens",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "preco_por_km",
                table: "gestao_viagens",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "cliente_id",
                table: "faturas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pdf_url",
                table: "faturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "viagem_id",
                table: "faturas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_cliente_id",
                table: "gestao_viagens",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cliente_id",
                table: "faturas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_viagem_id",
                table: "faturas",
                column: "viagem_id");

            migrationBuilder.AddForeignKey(
                name: "FK_faturas_clientes_catalogo_cliente_id",
                table: "faturas",
                column: "cliente_id",
                principalTable: "clientes_catalogo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_faturas_gestao_viagens_viagem_id",
                table: "faturas",
                column: "viagem_id",
                principalTable: "gestao_viagens",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gestao_viagens_clientes_catalogo_cliente_id",
                table: "gestao_viagens",
                column: "cliente_id",
                principalTable: "clientes_catalogo",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_faturas_clientes_catalogo_cliente_id",
                table: "faturas");

            migrationBuilder.DropForeignKey(
                name: "FK_faturas_gestao_viagens_viagem_id",
                table: "faturas");

            migrationBuilder.DropForeignKey(
                name: "FK_gestao_viagens_clientes_catalogo_cliente_id",
                table: "gestao_viagens");

            migrationBuilder.DropIndex(
                name: "IX_gestao_viagens_cliente_id",
                table: "gestao_viagens");

            migrationBuilder.DropIndex(
                name: "IX_faturas_cliente_id",
                table: "faturas");

            migrationBuilder.DropIndex(
                name: "IX_faturas_viagem_id",
                table: "faturas");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "gestao_viagens");

            migrationBuilder.DropColumn(
                name: "destino",
                table: "gestao_viagens");

            migrationBuilder.DropColumn(
                name: "origem",
                table: "gestao_viagens");

            migrationBuilder.DropColumn(
                name: "preco_por_km",
                table: "gestao_viagens");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "faturas");

            migrationBuilder.DropColumn(
                name: "pdf_url",
                table: "faturas");

            migrationBuilder.DropColumn(
                name: "viagem_id",
                table: "faturas");
        }
    }
}
