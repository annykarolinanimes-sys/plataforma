using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("documentos_gerais")]
public class DocumentoGeral
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_documento"), MaxLength(50)]
    public string NumeroDocumento { get; set; } = string.Empty;

    [Column("tipo"), MaxLength(50)]
    public string Tipo { get; set; } = string.Empty;

    [Column("nome"), MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("data_documento")]
    public DateTime DataDocumento { get; set; } = DateTime.UtcNow;

    [Column("data_criacao")]
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    [Column("caminho_ficheiro")]
    public string? CaminhoFicheiro { get; set; }

    [Column("tamanho_bytes")]
    public long TamanhoBytes { get; set; }

    [Column("entidade_relacionada"), MaxLength(100)]
    public string? EntidadeRelacionada { get; set; }

    [Column("entidade_id")]
    public int? EntidadeId { get; set; }

    [Column("tags"), MaxLength(500)]
    public string? Tags { get; set; }

    [Column("categoria"), MaxLength(100)]
    public string? Categoria { get; set; }

    [Column("favorito")]
    public bool Favorito { get; set; }

    [Column("visualizacoes")]
    public int Visualizacoes { get; set; }

    [Column("downloads")]
    public int Downloads { get; set; }

    [Column("ultimo_acesso")]
    public DateTime? UltimoAcesso { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    // Auditoria
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User? Usuario { get; set; }

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}