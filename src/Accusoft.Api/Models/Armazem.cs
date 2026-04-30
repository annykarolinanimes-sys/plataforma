using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("armazens")]
public class Armazem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Código é obrigatório.")]
    [Column("codigo"), MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Localização é obrigatória.")]
    [Column("localizacao"), MaxLength(10)]
    [RegularExpression(@"^[A-Z]\d{1,2}-\d{2}$", ErrorMessage = "Localização deve estar no formato A0-00 (ex: A1-01, B12-34).")]
    public string Localizacao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nome é obrigatório.")]
    [Column("nome"), MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Column("tipo"), MaxLength(50)]
    public string? Tipo { get; set; } = "principal";

    [Column("morada"), MaxLength(300)]
    public string? Morada { get; set; }

    [Column("codigo_postal"), MaxLength(20)]
    [RegularExpression(@"^\d{4}-\d{3}$", ErrorMessage = "Código postal deve estar no formato XXXX-XXX.")]
    public string? CodigoPostal { get; set; }

    [Column("pais"), MaxLength(100)]
    public string? Pais { get; set; } = "Portugal";

    [Column("email"), MaxLength(200)]
    public string? Email { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_por")]
    public int CriadoPor { get; set; }

    [ForeignKey(nameof(CriadoPor))]
    public User? CriadoPorUtilizador { get; set; }

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}