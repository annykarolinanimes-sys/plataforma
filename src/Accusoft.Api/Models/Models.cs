using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;



public enum UserRole   { Admin, User }
public enum UserStatus { Ativo, Inativo }

public enum EnvioEstado  { Pendente, Entregue, Atraso, Cancelado }
public enum DocTipo      { Pdf, Docx, Xlsx, Imagem, Arquivo, Outro }
public enum AlertaTipo   { Documento, Envio, Sistema }

public enum MovimentacaoTipo
{
    MovimentacaoInterna,
    ReposicaoPicking,
    Ajuste,
    InventarioParcial,
    Entrada,
    Saida,
}


public record SalvarRascunhoRececaoRequest(string ReferenciaDocumento, object Linhas);
public record AtualizarRascunhoRececaoRequest(string? ReferenciaDocumento, object? Linhas);
public record SalvarRascunhoExpedicaoRequest(string NumeroEncomenda, string Transportadora, string Destinatario, object Linhas);
public record AtualizarRascunhoExpedicaoRequest(string? NumeroEncomenda, string? Transportadora, string? Destinatario, object? Linhas);


[Table("users")]
public class User
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("nome"), MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Column("email"), MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("senha_hash")]
    public string SenhaHash { get; set; } = string.Empty;

    [Column("role")]
    public UserRole Role { get; set; }

    [Column("status")]
    public UserStatus Status { get; set; }

    [Column("departamento"), MaxLength(100)]
    public string? Departamento { get; set; }

    [Column("cargo"), MaxLength(100)]
    public string? Cargo { get; set; }

    [Column("telefone"), MaxLength(20)]
    public string? Telefone { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("data_criacao")]
    public DateTimeOffset DataCriacao { get; set; } = DateTimeOffset.UtcNow;

    [Column("ultimo_login")]
    public DateTimeOffset? UltimoLogin { get; set; }

    public ICollection<AuditLog>? AuditLogs { get; set; }
    public ICollection<Alerta>?   Alertas   { get; set; }
}


[Table("envios")]
public class Envio
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("id_string"), MaxLength(30)]
    public string IdString { get; set; } = string.Empty;

    [Column("nome_equipamento"), MaxLength(300)]
    public string NomeEquipamento { get; set; } = string.Empty;

    [Column("data_prevista")]
    public DateOnly DataPrevista { get; set; }

    [Column("estado")]
    public EnvioEstado Estado { get; set; } = EnvioEstado.Pendente;

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("data_criacao")]
    public DateTimeOffset DataCriacao { get; set; } = DateTimeOffset.UtcNow;

    [Column("data_atualizacao")]
    public DateTimeOffset DataAtualizacao { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Documento> Documentos { get; set; } = [];
    public ICollection<Alerta>    Alertas    { get; set; } = [];
}

[Table("documentos")]
public class Documento
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("nome"), MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    [Column("path_url")]
    public string PathUrl { get; set; } = string.Empty;

    [Column("tipo")]
    public DocTipo Tipo { get; set; } = DocTipo.Outro;

    [Column("tamanho_bytes")]
    public long TamanhoBytes { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("envio_id")]
    public int? EnvioId { get; set; }

    [ForeignKey(nameof(EnvioId))]
    public Envio? Envio { get; set; }

    [Column("data_upload")]
    public DateTimeOffset DataUpload { get; set; } = DateTimeOffset.UtcNow;

    [Column("data_abertura")]
    public DateTimeOffset? DataAbertura { get; set; }

    public ICollection<Alerta> Alertas { get; set; } = [];
}

[Table("alertas")]
public class Alerta
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("tipo")]
    public AlertaTipo Tipo { get; set; } = AlertaTipo.Sistema;

    [Column("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [Column("detalhe")]
    public string? Detalhe { get; set; }

    [Column("lido")]
    public bool Lido { get; set; } = false;

    [Column("data")]
    public DateTimeOffset Data { get; set; } = DateTimeOffset.UtcNow;

    // NOTA: Se no futuro quiser ligar alertas a envios/documentos,
    // adicione as colunas primeiro no SQL com uma migration:
    //   ALTER TABLE alertas ADD COLUMN envio_id INT REFERENCES envios(id);
    //   ALTER TABLE alertas ADD COLUMN documento_id INT REFERENCES documentos(id);
    // Só depois descomente as propriedades abaixo:
    //
    // [Column("envio_id")]
    // public int? EnvioId { get; set; }
    // [ForeignKey(nameof(EnvioId))]
    // public Envio? Envio { get; set; }
    //
    // [Column("documento_id")]
    // public int? DocumentoId { get; set; }
    // [ForeignKey(nameof(DocumentoId))]
    // public Documento? Documento { get; set; }
}


[Table("audit_logs")]
public class AuditLog
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("admin_id")]
    public int AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public User Admin { get; set; } = null!;

    [Column("acao"), MaxLength(100)]
    public string Acao { get; set; } = string.Empty;

    [Column("detalhe")]
    public string? Detalhe { get; set; }

    [Column("ip_address"), MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}


[Table("rececao_rascunhos")]
public class RececaoRascunho
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("referencia_documento"), MaxLength(100)]
    public string ReferenciaDocumento { get; set; } = string.Empty;

    [Column("linhas_json", TypeName = "jsonb")]
    public string LinhasJson { get; set; } = "[]";

    [Column("finalizado")]
    public bool Finalizado { get; set; } = false;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}

[Table("expedicao_rascunhos")]
public class ExpedicaoRascunho
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("numero_encomenda"), MaxLength(100)]
    public string NumeroEncomenda { get; set; } = string.Empty;

    [Column("transportadora"), MaxLength(100)]
    public string Transportadora { get; set; } = string.Empty;

    [Column("destinatario"), MaxLength(200)]
    public string Destinatario { get; set; } = string.Empty;

    [Column("linhas_json", TypeName = "jsonb")]
    public string LinhasJson { get; set; } = "[]";

    [Column("finalizado")]
    public bool Finalizado { get; set; } = false;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}

// ═══════════════════════════════════════════════════════════════════════════════
// NOTA IMPORTANTE — Classe "Produtos" REMOVIDA de propósito
// ═══════════════════════════════════════════════════════════════════════════════
// O ficheiro Models.cs continha [Table("produto")] class Produtos que causava
// dois problemas críticos:
//
//   1. [Table("produto")] — tabela errada; a BD chama-se "produtos" (com 's')
//   2. Duplicado de Produto.cs que usa [Table("produtos")] — o EF ficava
//      com dois tipos mapeados para nomes parecidos, causando conflito
//   3. Usava a coluna "min_level" que NÃO existe na tabela "produtos" do SQL
//
// A classe canónica é Produto em Produto.cs. Use sempre essa.
