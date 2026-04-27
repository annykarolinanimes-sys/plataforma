using System;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocoloToOrdemCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:user_status", "ativo,inativo")
                .Annotation("Npgsql:Enum:user_role", "admin,user")
                .Annotation("Npgsql:Enum:envio_estado", "pendente,entregue,atraso,cancelado")
                .Annotation("Npgsql:Enum:doc_tipo", "pdf,docx,xlsx,imagem,arquivo,outro")
                .Annotation("Npgsql:Enum:movimentacao_tipo", "movimentacao_interna,reposicao_picking,ajuste,inventario_parcial,entrada,saida")
                .Annotation("Npgsql:Enum:alerta_tipo", "documento,envio,sistema");


            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contacto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    min_level = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    senha_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    status = table.Column<UserStatus>(type: "user_status", nullable: false),
                    departamento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_login = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "estoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    warehouse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    reserved_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    picking_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estoque", x => x.id);
                    table.ForeignKey(
                        name: "FK_estoque_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_id = table.Column<int>(type: "integer", nullable: false),
                    acao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detalhe = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_admin_id",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_string = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nome_equipamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<EnvioEstado>(type: "envio_estado", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_envios", x => x.id);
                    table.ForeignKey(
                        name: "FK_envios_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expedicao_rascunhos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    numero_encomenda = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transportadora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    linhas_json = table.Column<string>(type: "jsonb", nullable: false),
                    finalizado = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expedicao_rascunhos", x => x.id);
                    table.ForeignKey(
                        name: "FK_expedicao_rascunhos_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faturas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_fatura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cliente_contacto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cliente_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cliente_morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cliente_nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    data_doc = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quem_executou = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    horas_trabalho = table.Column<decimal>(type: "numeric", nullable: true),
                    material_utilizado = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_faturas_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lpns",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    armazem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    localizacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lpns", x => x.id);
                    table.ForeignKey(
                        name: "FK_lpns_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ordens_compra",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_oc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fornecedor_id = table.Column<int>(type: "integer", nullable: false),
                    data_emissao = table.Column<DateOnly>(type: "date", nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    protocolo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_compra", x => x.id);
                    table.ForeignKey(
                        name: "FK_ordens_compra_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordens_compra_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rececao_rascunhos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    referencia_documento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    linhas_json = table.Column<string>(type: "jsonb", nullable: false),
                    finalizado = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rececao_rascunhos", x => x.id);
                    table.ForeignKey(
                        name: "FK_rececao_rascunhos_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movimentacoes_estoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    estoque_id = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    data_mov = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacoes_estoque", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_estoque_estoque_id",
                        column: x => x.estoque_id,
                        principalTable: "estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    path_url = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<DocTipo>(type: "doc_tipo", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    envio_id = table.Column<int>(type: "integer", nullable: true),
                    data_upload = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_abertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_documentos_envios_envio_id",
                        column: x => x.envio_id,
                        principalTable: "envios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documentos_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fatura_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fatura_id = table.Column<int>(type: "integer", nullable: false),
                    marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fatura_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_fatura_itens_faturas_fatura_id",
                        column: x => x.fatura_id,
                        principalTable: "faturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entradas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_entrada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ordem_compra_id = table.Column<int>(type: "integer", nullable: true),
                    fornecedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_entrada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_entrada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    protocolo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zona_recepcao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lpn_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rtm_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acomodado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entradas", x => x.id);
                    table.ForeignKey(
                        name: "FK_entradas_lpns_lpn_id",
                        column: x => x.lpn_id,
                        principalTable: "lpns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_entradas_ordens_compra_ordem_compra_id",
                        column: x => x.ordem_compra_id,
                        principalTable: "ordens_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_entradas_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ordens_compra_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ordem_compra_id = table.Column<int>(type: "integer", nullable: false),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    quantidade_recebida = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_compra_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_ordens_compra_itens_ordens_compra_ordem_compra_id",
                        column: x => x.ordem_compra_id,
                        principalTable: "ordens_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ordens_compra_itens_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<AlertaTipo>(type: "alerta_tipo", nullable: false),
                    mensagem = table.Column<string>(type: "text", nullable: false),
                    detalhe = table.Column<string>(type: "text", nullable: true),
                    lido = table.Column<bool>(type: "boolean", nullable: false),
                    data = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    envio_id = table.Column<int>(type: "integer", nullable: true),
                    documento_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.id);
                    table.ForeignKey(
                        name: "FK_alertas_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_alertas_envios_envio_id",
                        column: x => x.envio_id,
                        principalTable: "envios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_alertas_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devolucoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_devolucao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entrada_id = table.Column<int>(type: "integer", nullable: true),
                    ordem_compra_id = table.Column<int>(type: "integer", nullable: true),
                    fornecedor_id = table.Column<int>(type: "integer", nullable: false),
                    data_devolucao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_geral = table.Column<string>(type: "text", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    valor_total = table.Column<decimal>(type: "numeric", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devolucoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_devolucoes_entradas_entrada_id",
                        column: x => x.entrada_id,
                        principalTable: "entradas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_devolucoes_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_devolucoes_ordens_compra_ordem_compra_id",
                        column: x => x.ordem_compra_id,
                        principalTable: "ordens_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_devolucoes_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entrada_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entrada_id = table.Column<int>(type: "integer", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    produto_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantidade_esperada = table.Column<int>(type: "integer", nullable: false),
                    quantidade_contada = table.Column<int>(type: "integer", nullable: false),
                    quantidade_aceite = table.Column<int>(type: "integer", nullable: false),
                    quantidade_devolvida = table.Column<int>(type: "integer", nullable: false),
                    lote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    validade = table.Column<DateOnly>(type: "date", nullable: true),
                    localizacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    armazem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    lpn_id = table.Column<int>(type: "integer", nullable: true),
                    conferido = table.Column<bool>(type: "boolean", nullable: false),
                    tem_divergencia = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_divergencia = table.Column<string>(type: "text", nullable: true),
                    nao_conformidade = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_nao_conformidade = table.Column<string>(type: "text", nullable: true),
                    diferenca = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrada_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_entrada_itens_entradas_entrada_id",
                        column: x => x.entrada_id,
                        principalTable: "entradas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entrada_itens_lpns_lpn_id",
                        column: x => x.lpn_id,
                        principalTable: "lpns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "devolucao_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    devolucao_id = table.Column<int>(type: "integer", nullable: false),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    quantidade_aprovada = table.Column<int>(type: "integer", nullable: false),
                    quantidade_rejeitada = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    lote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    localizacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devolucao_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_devolucao_itens_devolucoes_devolucao_id",
                        column: x => x.devolucao_id,
                        principalTable: "devolucoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_devolucao_itens_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_documento_id",
                table: "alertas",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_envio_id",
                table: "alertas",
                column: "envio_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_usuario_id",
                table: "alertas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_admin_id",
                table: "audit_logs",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucao_itens_devolucao_id",
                table: "devolucao_itens",
                column: "devolucao_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucao_itens_produto_id",
                table: "devolucao_itens",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_entrada_id",
                table: "devolucoes",
                column: "entrada_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_fornecedor_id",
                table: "devolucoes",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_numero_devolucao",
                table: "devolucoes",
                column: "numero_devolucao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_ordem_compra_id",
                table: "devolucoes",
                column: "ordem_compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_status",
                table: "devolucoes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_devolucoes_usuario_id",
                table: "devolucoes",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_envio_id",
                table: "documentos",
                column: "envio_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_usuario_id",
                table: "documentos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrada_itens_entrada_id",
                table: "entrada_itens",
                column: "entrada_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrada_itens_lpn_id",
                table: "entrada_itens",
                column: "lpn_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrada_itens_sku",
                table: "entrada_itens",
                column: "sku");

            migrationBuilder.CreateIndex(
                name: "IX_entradas_lpn_id",
                table: "entradas",
                column: "lpn_id");

            migrationBuilder.CreateIndex(
                name: "IX_entradas_numero_entrada",
                table: "entradas",
                column: "numero_entrada",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entradas_ordem_compra_id",
                table: "entradas",
                column: "ordem_compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_entradas_rtm_status",
                table: "entradas",
                column: "rtm_status");

            migrationBuilder.CreateIndex(
                name: "IX_entradas_status",
                table: "entradas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_entradas_usuario_id",
                table: "entradas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_envios_id_string",
                table: "envios",
                column: "id_string",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_envios_usuario_id",
                table: "envios",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_estoque_produto_id_warehouse_location_lot_number",
                table: "estoque",
                columns: new[] { "produto_id", "warehouse", "location", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expedicao_rascunhos_usuario_id",
                table: "expedicao_rascunhos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_fatura_itens_fatura_id",
                table: "fatura_itens",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_usuario_id",
                table: "faturas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_nif",
                table: "fornecedores",
                column: "nif");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_nome",
                table: "fornecedores",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "IX_lpns_codigo",
                table: "lpns",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lpns_status",
                table: "lpns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_lpns_usuario_id",
                table: "lpns",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_estoque_id",
                table: "movimentacoes_estoque",
                column: "estoque_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_produto_id",
                table: "movimentacoes_estoque",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_usuario_id",
                table: "movimentacoes_estoque",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_fornecedor_id",
                table: "ordens_compra",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_numero_oc",
                table: "ordens_compra",
                column: "numero_oc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_status",
                table: "ordens_compra",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_usuario_id",
                table: "ordens_compra",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_itens_ordem_compra_id",
                table: "ordens_compra_itens",
                column: "ordem_compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_itens_produto_id",
                table: "ordens_compra_itens",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_rececao_rascunhos_usuario_id",
                table: "rececao_rascunhos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "devolucao_itens");

            migrationBuilder.DropTable(
                name: "entrada_itens");

            migrationBuilder.DropTable(
                name: "expedicao_rascunhos");

            migrationBuilder.DropTable(
                name: "fatura_itens");

            migrationBuilder.DropTable(
                name: "movimentacoes_estoque");

            migrationBuilder.DropTable(
                name: "ordens_compra_itens");

            migrationBuilder.DropTable(
                name: "rececao_rascunhos");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "devolucoes");

            migrationBuilder.DropTable(
                name: "faturas");

            migrationBuilder.DropTable(
                name: "estoque");

            migrationBuilder.DropTable(
                name: "envios");

            migrationBuilder.DropTable(
                name: "entradas");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "lpns");

            migrationBuilder.DropTable(
                name: "ordens_compra");

            migrationBuilder.DropTable(
                name: "fornecedores");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
