using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("clientes_catalogo")]
public class ClienteCatalogo
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("codigo"), MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("nome"), MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Column("contribuinte"), MaxLength(20)]
    public string? Contribuinte { get; set; }

    [Column("telefone"), MaxLength(30)]
    public string? Telefone { get; set; }

    [Column("email"), MaxLength(200)]
    public string? Email { get; set; }

    [Column("morada"), MaxLength(300)]
    public string? Morada { get; set; }

    [Column("localidade"), MaxLength(100)]
    public string? Localidade { get; set; }

    [Column("codigo_postal"), MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [Column("pais"), MaxLength(100)]
    public string? Pais { get; set; } = "Portugal";

    [Column("contacto_nome"), MaxLength(150)]
    public string? ContactoNome { get; set; }

    [Column("contacto_telefone"), MaxLength(30)]
    public string? ContactoTelefone { get; set; }

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