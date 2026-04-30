using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizacaoToArmazem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "peso_unitario",
                table: "produtos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "volume_unitario",
                table: "produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "localizacao",
                table: "armazens",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "atribuicoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_atribuicao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_atribuicao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cliente_contacto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_origem = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    endereco_destino = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    data_prevista_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_prevista_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    motorista_id = table.Column<int>(type: "integer", nullable: true),
                    veiculo_id = table.Column<int>(type: "integer", nullable: true),
                    transportadora_id = table.Column<int>(type: "integer", nullable: true),
                    rota_id = table.Column<int>(type: "integer", nullable: true),
                    distancia_total_km = table.Column<decimal>(type: "numeric", nullable: false),
                    tempo_estimado_horas = table.Column<decimal>(type: "numeric", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atribuicoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_atribuicoes_rotas_rota_id",
                        column: x => x.rota_id,
                        principalTable: "rotas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_atribuicoes_transportadoras_transportadora_id",
                        column: x => x.transportadora_id,
                        principalTable: "transportadoras",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_atribuicoes_users_motorista_id",
                        column: x => x.motorista_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_atribuicoes_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_atribuicoes_veiculos_veiculo_id",
                        column: x => x.veiculo_id,
                        principalTable: "veiculos",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "gestao_viagens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_viagem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_inicio_planeada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_fim_planeada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_inicio_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_fim_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rota_id = table.Column<int>(type: "integer", nullable: true),
                    veiculo_id = table.Column<int>(type: "integer", nullable: true),
                    motorista_id = table.Column<int>(type: "integer", nullable: true),
                    transportadora_id = table.Column<int>(type: "integer", nullable: true),
                    carga_descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    carga_peso = table.Column<decimal>(type: "numeric", nullable: false),
                    carga_volume = table.Column<int>(type: "integer", nullable: false),
                    carga_observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    distancia_total_km = table.Column<decimal>(type: "numeric", nullable: false),
                    distancia_percorrida_km = table.Column<decimal>(type: "numeric", nullable: false),
                    tempo_estimado_horas = table.Column<decimal>(type: "numeric", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gestao_viagens", x => x.id);
                    table.ForeignKey(
                        name: "FK_gestao_viagens_rotas_rota_id",
                        column: x => x.rota_id,
                        principalTable: "rotas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gestao_viagens_transportadoras_transportadora_id",
                        column: x => x.transportadora_id,
                        principalTable: "transportadoras",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gestao_viagens_users_motorista_id",
                        column: x => x.motorista_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gestao_viagens_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gestao_viagens_veiculos_veiculo_id",
                        column: x => x.veiculo_id,
                        principalTable: "veiculos",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "atribuicao_ajudantes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: false),
                    ajudante_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atribuicao_ajudantes", x => x.id);
                    table.ForeignKey(
                        name: "FK_atribuicao_ajudantes_atribuicoes_atribuicao_id",
                        column: x => x.atribuicao_id,
                        principalTable: "atribuicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_atribuicao_ajudantes_users_ajudante_id",
                        column: x => x.ajudante_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "atribuicao_entregas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    contacto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    realizada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atribuicao_entregas", x => x.id);
                    table.ForeignKey(
                        name: "FK_atribuicao_entregas_atribuicoes_atribuicao_id",
                        column: x => x.atribuicao_id,
                        principalTable: "atribuicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fechos_viagem",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_fecho = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: false),
                    data_fecho = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_inicio_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_fim_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    combustivel_litros = table.Column<decimal>(type: "numeric", nullable: true),
                    combustivel_custo = table.Column<decimal>(type: "numeric", nullable: true),
                    portagens_custo = table.Column<decimal>(type: "numeric", nullable: true),
                    outros_custos = table.Column<decimal>(type: "numeric", nullable: true),
                    custos_extras_descricao = table.Column<string>(type: "text", nullable: true),
                    custo_total = table.Column<decimal>(type: "numeric", nullable: false),
                    quilometros_inicio = table.Column<int>(type: "integer", nullable: true),
                    quilometros_fim = table.Column<int>(type: "integer", nullable: true),
                    quilometros_percorridos = table.Column<int>(type: "integer", nullable: true),
                    entregas_nao_realizadas_ids = table.Column<string>(type: "text", nullable: true),
                    entregas_pendentes_obs = table.Column<string>(type: "text", nullable: true),
                    tem_incidentes = table.Column<bool>(type: "boolean", nullable: false),
                    incidentes_descricao = table.Column<string>(type: "text", nullable: true),
                    faturado = table.Column<bool>(type: "boolean", nullable: false),
                    fatura_id = table.Column<int>(type: "integer", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "guias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_guia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_emissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: true),
                    cliente_id = table.Column<int>(type: "integer", nullable: true),
                    transportadora_id = table.Column<int>(type: "integer", nullable: true),
                    endereco_origem = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    endereco_destino = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    total_itens = table.Column<int>(type: "integer", nullable: false),
                    peso_total_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    volume_total_m3 = table.Column<int>(type: "integer", nullable: false),
                    total_volumes = table.Column<int>(type: "integer", nullable: false),
                    data_prevista_entrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_entrega_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    instrucoes_especiais = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guias", x => x.id);
                    table.ForeignKey(
                        name: "FK_guias_atribuicoes_atribuicao_id",
                        column: x => x.atribuicao_id,
                        principalTable: "atribuicoes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_guias_clientes_catalogo_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes_catalogo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_guias_transportadoras_transportadora_id",
                        column: x => x.transportadora_id,
                        principalTable: "transportadoras",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_guias_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incidentes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_incidente = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_ocorrencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gravidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    viagem_id = table.Column<int>(type: "integer", nullable: true),
                    veiculo_id = table.Column<int>(type: "integer", nullable: true),
                    cliente_id = table.Column<int>(type: "integer", nullable: true),
                    atribuicao_id = table.Column<int>(type: "integer", nullable: true),
                    data_resolucao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    causa = table.Column<string>(type: "text", nullable: true),
                    acao_corretiva = table.Column<string>(type: "text", nullable: true),
                    responsavel_resolucao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    custo_associado = table.Column<decimal>(type: "numeric", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidentes", x => x.id);
                    table.ForeignKey(
                        name: "FK_incidentes_atribuicoes_atribuicao_id",
                        column: x => x.atribuicao_id,
                        principalTable: "atribuicoes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_incidentes_clientes_catalogo_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes_catalogo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_incidentes_gestao_viagens_viagem_id",
                        column: x => x.viagem_id,
                        principalTable: "gestao_viagens",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_incidentes_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incidentes_veiculos_veiculo_id",
                        column: x => x.veiculo_id,
                        principalTable: "veiculos",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "guia_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guia_id = table.Column<int>(type: "integer", nullable: false),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    peso_unitario = table.Column<decimal>(type: "numeric", nullable: false),
                    peso_total = table.Column<decimal>(type: "numeric", nullable: false),
                    volume_unitario = table.Column<int>(type: "integer", nullable: false),
                    volume_total = table.Column<int>(type: "integer", nullable: false),
                    lote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guia_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_guia_itens_guias_guia_id",
                        column: x => x.guia_id,
                        principalTable: "guias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guia_itens_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_atribuicao_ajudantes_ajudante_id",
                table: "atribuicao_ajudantes",
                column: "ajudante_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicao_ajudantes_atribuicao_id",
                table: "atribuicao_ajudantes",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicao_entregas_atribuicao_id",
                table: "atribuicao_entregas",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicoes_motorista_id",
                table: "atribuicoes",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicoes_rota_id",
                table: "atribuicoes",
                column: "rota_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicoes_transportadora_id",
                table: "atribuicoes",
                column: "transportadora_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicoes_usuario_id",
                table: "atribuicoes",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_atribuicoes_veiculo_id",
                table: "atribuicoes",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "IX_fechos_viagem_atribuicao_id",
                table: "fechos_viagem",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_fechos_viagem_usuario_id",
                table: "fechos_viagem",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_motorista_id",
                table: "gestao_viagens",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_rota_id",
                table: "gestao_viagens",
                column: "rota_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_transportadora_id",
                table: "gestao_viagens",
                column: "transportadora_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_usuario_id",
                table: "gestao_viagens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestao_viagens_veiculo_id",
                table: "gestao_viagens",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "IX_guia_itens_guia_id",
                table: "guia_itens",
                column: "guia_id");

            migrationBuilder.CreateIndex(
                name: "IX_guia_itens_produto_id",
                table: "guia_itens",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_guias_atribuicao_id",
                table: "guias",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_guias_cliente_id",
                table: "guias",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_guias_transportadora_id",
                table: "guias",
                column: "transportadora_id");

            migrationBuilder.CreateIndex(
                name: "IX_guias_usuario_id",
                table: "guias",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidentes_atribuicao_id",
                table: "incidentes",
                column: "atribuicao_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidentes_cliente_id",
                table: "incidentes",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidentes_usuario_id",
                table: "incidentes",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidentes_veiculo_id",
                table: "incidentes",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidentes_viagem_id",
                table: "incidentes",
                column: "viagem_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atribuicao_ajudantes");

            migrationBuilder.DropTable(
                name: "atribuicao_entregas");

            migrationBuilder.DropTable(
                name: "fechos_viagem");

            migrationBuilder.DropTable(
                name: "guia_itens");

            migrationBuilder.DropTable(
                name: "incidentes");

            migrationBuilder.DropTable(
                name: "guias");

            migrationBuilder.DropTable(
                name: "gestao_viagens");

            migrationBuilder.DropTable(
                name: "atribuicoes");

            migrationBuilder.DropColumn(
                name: "peso_unitario",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "volume_unitario",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "localizacao",
                table: "armazens");
        }
    }
}
