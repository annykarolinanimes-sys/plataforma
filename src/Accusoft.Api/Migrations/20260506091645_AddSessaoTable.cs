using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessaoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechos_viagem");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_user_id = table.Column<int>(type: "integer", nullable: false),
                    to_user_id = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_messages_users_from_user_id",
                        column: x => x.from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_messages_users_to_user_id",
                        column: x => x.to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_jwt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_atividade = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_expiracao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessoes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_from_user_id",
                table: "chat_messages",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_to_user_id",
                table: "chat_messages",
                column: "to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessoes_user_id",
                table: "sessoes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "sessoes");

            migrationBuilder.CreateTable(
                name: "fechos_viagem",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    combustivel_custo = table.Column<decimal>(type: "numeric", nullable: true),
                    combustivel_litros = table.Column<decimal>(type: "numeric", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    custo_total = table.Column<decimal>(type: "numeric", nullable: false),
                    custos_extras_descricao = table.Column<string>(type: "text", nullable: true),
                    data_fecho = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_fim_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_inicio_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    entregas_nao_realizadas_ids = table.Column<string>(type: "text", nullable: true),
                    entregas_pendentes_obs = table.Column<string>(type: "text", nullable: true),
                    fatura_id = table.Column<int>(type: "integer", nullable: true),
                    faturado = table.Column<bool>(type: "boolean", nullable: false),
                    incidentes_descricao = table.Column<string>(type: "text", nullable: true),
                    numero_fecho = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    outros_custos = table.Column<decimal>(type: "numeric", nullable: true),
                    portagens_custo = table.Column<decimal>(type: "numeric", nullable: true),
                    quilometros_fim = table.Column<int>(type: "integer", nullable: true),
                    quilometros_inicio = table.Column<int>(type: "integer", nullable: true),
                    quilometros_percorridos = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tem_incidentes = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fechos_viagem", x => x.id);
                    table.ForeignKey(
                        name: "FK_fechos_viagem_atribuicoes_atribuicao_id",
                        column: x => x.atribuicao_id,
                        principalTable: "atribuicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fechos_viagem_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fechos_viagem_atribuicao_id",
                table: "fechos_viagem",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_fechos_viagem_usuario_id",
                table: "fechos_viagem",
                column: "usuario_id");
        }
    }
}
