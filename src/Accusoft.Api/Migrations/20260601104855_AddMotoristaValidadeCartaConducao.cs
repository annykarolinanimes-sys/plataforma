using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMotoristaValidadeCartaConducao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "veiculo_anexos");

            migrationBuilder.AddColumn<DateOnly>(
                name: "validade_carta_conducao",
                table: "motoristas",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_motoristas_carta_conducao",
                table: "motoristas",
                column: "carta_conducao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_motoristas_carta_conducao",
                table: "motoristas");

            migrationBuilder.DropColumn(
                name: "validade_carta_conducao",
                table: "motoristas");

            migrationBuilder.CreateTable(
                name: "veiculo_anexos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    veiculo_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nome_ficheiro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_original = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    path_url = table.Column<string>(type: "text", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo_anexos", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculo_anexos_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_veiculo_anexos_veiculos_veiculo_id",
                        column: x => x.veiculo_id,
                        principalTable: "veiculos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_anexos_usuario_id",
                table: "veiculo_anexos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_anexos_veiculo_id",
                table: "veiculo_anexos",
                column: "veiculo_id");
        }
    }
}
