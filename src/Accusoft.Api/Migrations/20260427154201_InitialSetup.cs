using System;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accusoft.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:alerta_tipo", "documento,envio,sistema")
                .Annotation("Npgsql:Enum:movimentacao_tipo", "movimentacao_interna,reposicao_picking,ajuste,inventario_parcial,entrada,saida")
                .Annotation("Npgsql:Enum:user_role", "admin,user")
                .Annotation("Npgsql:Enum:user_status", "ativo,inativo");

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
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_login = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
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
                    data = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.id);
                    table.ForeignKey(
                        name: "FK_alertas_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "armazens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    localidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pais = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    responsavel_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    responsavel_telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_armazens", x => x.id);
                    table.ForeignKey(
                        name: "FK_armazens_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "clientes_catalogo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contribuinte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    localidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pais = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contacto_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contacto_telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes_catalogo", x => x.id);
                    table.ForeignKey(
                        name: "FK_clientes_catalogo_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    localidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pais = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contacto_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contacto_telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedores", x => x.id);
                    table.ForeignKey(
                        name: "FK_fornecedores_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "transportadoras",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    localidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pais = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contacto_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contacto_telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transportadoras", x => x.id);
                    table.ForeignKey(
                        name: "FK_transportadoras_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "veiculos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    vin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo_combustivel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cilindrada = table.Column<int>(type: "integer", nullable: true),
                    potencia = table.Column<int>(type: "integer", nullable: true),
                    lugares = table.Column<int>(type: "integer", nullable: true),
                    peso = table.Column<int>(type: "integer", nullable: true),
                    proprietario_id = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    criado_por = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculos", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculos_clientes_catalogo_proprietario_id",
                        column: x => x.proprietario_id,
                        principalTable: "clientes_catalogo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_veiculos_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fornecedor_id = table.Column<int>(type: "integer", nullable: true),
                    preco_compra = table.Column<decimal>(type: "numeric(15,4)", nullable: false),
                    preco_venda = table.Column<decimal>(type: "numeric(15,4)", nullable: false),
                    iva = table.Column<int>(type: "integer", nullable: false),
                    stock_atual = table.Column<int>(type: "integer", nullable: false),
                    stock_minimo = table.Column<int>(type: "integer", nullable: false),
                    unidade_medida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    localizacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lote_obrigatorio = table.Column<bool>(type: "boolean", nullable: false),
                    validade_obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos", x => x.id);
                    table.ForeignKey(
                        name: "FK_produtos_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_produtos_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rotas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    distancia_km = table.Column<decimal>(type: "numeric", nullable: true),
                    tempo_estimado_min = table.Column<int>(type: "integer", nullable: true),
                    transportadora_id = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_por = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_rotas_transportadoras_transportadora_id",
                        column: x => x.transportadora_id,
                        principalTable: "transportadoras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rotas_users_criado_por",
                        column: x => x.criado_por,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "estoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produto_id = table.Column<int>(type: "integer", nullable: false),
                    armazem_id = table.Column<int>(type: "integer", nullable: true),
                    localizacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    validade = table.Column<DateOnly>(type: "date", nullable: true),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    quantidade_reservada = table.Column<int>(type: "integer", nullable: false),
                    quantidade_picking = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ultima_movimentacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estoque", x => x.id);
                    table.ForeignKey(
                        name: "FK_estoque_armazens_armazem_id",
                        column: x => x.armazem_id,
                        principalTable: "armazens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_estoque_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
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
                    tipo = table.Column<MovimentacaoTipo>(type: "movimentacao_tipo", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    origem_local = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    destino_local = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    armazem_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    data_mov = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstoqueId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacoes_estoque", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_armazens_armazem_id",
                        column: x => x.armazem_id,
                        principalTable: "armazens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_estoque_EstoqueId",
                        column: x => x.EstoqueId,
                        principalTable: "estoque",
                        principalColumn: "id");
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
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_usuario_id",
                table: "alertas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_armazens_codigo",
                table: "armazens",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_armazens_criado_por",
                table: "armazens",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_admin_id",
                table: "audit_logs",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_catalogo_codigo",
                table: "clientes_catalogo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_catalogo_criado_por",
                table: "clientes_catalogo",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_estoque_armazem_id",
                table: "estoque",
                column: "armazem_id");

            migrationBuilder.CreateIndex(
                name: "uq_estoque_produto_armazem_local_lote",
                table: "estoque",
                columns: new[] { "produto_id", "armazem_id", "localizacao", "lote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fatura_itens_fatura_id",
                table: "fatura_itens",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_numero_fatura",
                table: "faturas",
                column: "numero_fatura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faturas_usuario_id",
                table: "faturas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_codigo",
                table: "fornecedores",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_criado_por",
                table: "fornecedores",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_armazem_id",
                table: "movimentacoes_estoque",
                column: "armazem_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_EstoqueId",
                table: "movimentacoes_estoque",
                column: "EstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_produto_id",
                table: "movimentacoes_estoque",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_usuario_id",
                table: "movimentacoes_estoque",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_criado_por",
                table: "produtos",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_fornecedor_id",
                table: "produtos",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_sku",
                table: "produtos",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rotas_codigo",
                table: "rotas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rotas_criado_por",
                table: "rotas",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_rotas_transportadora_id",
                table: "rotas",
                column: "transportadora_id");

            migrationBuilder.CreateIndex(
                name: "IX_transportadoras_codigo",
                table: "transportadoras",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transportadoras_criado_por",
                table: "transportadoras",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculos_criado_por",
                table: "veiculos",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_veiculos_proprietario_id",
                table: "veiculos",
                column: "proprietario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "fatura_itens");

            migrationBuilder.DropTable(
                name: "movimentacoes_estoque");

            migrationBuilder.DropTable(
                name: "rotas");

            migrationBuilder.DropTable(
                name: "veiculos");

            migrationBuilder.DropTable(
                name: "faturas");

            migrationBuilder.DropTable(
                name: "estoque");

            migrationBuilder.DropTable(
                name: "transportadoras");

            migrationBuilder.DropTable(
                name: "clientes_catalogo");

            migrationBuilder.DropTable(
                name: "armazens");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "fornecedores");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
