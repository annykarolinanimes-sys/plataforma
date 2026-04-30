using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFieldsFromArmazem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "localidade",
                table: "armazens");

            migrationBuilder.DropColumn(
                name: "responsavel_nome",
                table: "armazens");

            migrationBuilder.DropColumn(
                name: "responsavel_telefone",
                table: "armazens");

            migrationBuilder.DropColumn(
                name: "telefone",
                table: "armazens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "localidade",
                table: "armazens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "responsavel_nome",
                table: "armazens",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "responsavel_telefone",
                table: "armazens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                table: "armazens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
