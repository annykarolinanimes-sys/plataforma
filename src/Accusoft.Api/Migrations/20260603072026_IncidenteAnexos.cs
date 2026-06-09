using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class IncidenteAnexos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidente_anexos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incidente_id = table.Column<int>(type: "integer", nullable: false),
                    nome_original = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_ficheiro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    path_url = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidente_anexos", x => x.id);
                    table.ForeignKey(
                        name: "FK_incidente_anexos_incidentes_incidente_id",
                        column: x => x.incidente_id,
                        principalTable: "incidentes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incidente_anexos_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidente_anexos_incidente_id",
                table: "incidente_anexos",
                column: "incidente_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidente_anexos_usuario_id",
                table: "incidente_anexos",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidente_anexos");
        }
    }
}
