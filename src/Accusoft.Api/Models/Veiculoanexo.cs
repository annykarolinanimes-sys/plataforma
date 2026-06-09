using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

/// <summary>
/// Representa um ficheiro anexo a um veículo — usado, entre outros, para
/// guardar o documento comprovativo de uma troca de matrícula.
/// </summary>
[Table("veiculo_anexos")]
public class VeiculoAnexo
{
    [Key, Column("id")]
    public int Id { get; set; }

    /// <summary>FK para o veículo ao qual este anexo pertence.</summary>
    [Column("veiculo_id")]
    public int VeiculoId { get; set; }

    [ForeignKey(nameof(VeiculoId))]
    public Veiculo Veiculo { get; set; } = null!;

    /// <summary>Nome original do ficheiro enviado pelo utilizador.</summary>
    [Column("nome_original"), MaxLength(300)]
    public string NomeOriginal { get; set; } = string.Empty;

    /// <summary>Nome gerado internamente (GUID + extensão) para evitar colisões.</summary>
    [Column("nome_ficheiro"), MaxLength(300)]
    public string NomeFicheiro { get; set; } = string.Empty;

    /// <summary>Caminho relativo do ficheiro no sistema de armazenamento.</summary>
    [Column("path_url")]
    public string PathUrl { get; set; } = string.Empty;

    /// <summary>Content-Type do ficheiro (ex: application/pdf, image/jpeg).</summary>
    [Column("mime_type"), MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Tamanho do ficheiro em bytes.</summary>
    [Column("tamanho_bytes")]
    public long TamanhoBytes { get; set; }

    /// <summary>Utilizador que fez o upload.</summary>
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}