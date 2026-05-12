using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("motoristas")]
public class Motorista
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("nome"), MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Column("telefone"), MaxLength(30)]
    public string Telefone { get; set; } = string.Empty;

    [Column("carta_conducao"), MaxLength(50)]
    public string CartaConducao { get; set; } = string.Empty;

    [Column("transportadora_id"), MaxLength(50)]
    public string TransportadoraId { get; set; } = string.Empty;

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