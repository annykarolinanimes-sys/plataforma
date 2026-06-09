using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("incidente_anexos")]
public class IncidenteAnexo
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("incidente_id")]
    public int IncidenteId { get; set; }

    [ForeignKey(nameof(IncidenteId))]
    public Incidente Incidente { get; set; } = null!;

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