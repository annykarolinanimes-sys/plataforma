using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoverColunaDocRferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "documento_referencia",
                table: "rececoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "documento_referencia",
                table: "rececoes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
