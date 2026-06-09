using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("fatura_anexos")]
public class FaturaAnexo
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("fatura_id")]
    public int FaturaId { get; set; }

    [ForeignKey(nameof(FaturaId))]
    public Invoice Fatura { get; set; } = null!;

    [Column("nome_original"), MaxLength(300)]
    public string NomeOriginal { get; set; } = string.Empty;

    [Column("nome_ficheiro"), MaxLength(300)]
    public string NomeFicheiro { get; set; } = string.Empty;

    [Column("path_url")]
    public string PathUrl { get; set; } = string.Empty;

    [Column("mime_type"), MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [Column("tamanho_bytes")]
    public long TamanhoBytes { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}