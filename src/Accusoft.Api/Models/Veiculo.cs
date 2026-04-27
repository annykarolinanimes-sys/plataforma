using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("veiculos")]
public class Veiculo
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("matricula"), MaxLength(20)]
    public string Matricula { get; set; } = string.Empty;

    [Column("marca"), MaxLength(100)]
    public string Marca { get; set; } = string.Empty;

    [Column("modelo"), MaxLength(100)]
    public string Modelo { get; set; } = string.Empty;

    [Column("cor"), MaxLength(50)]
    public string? Cor { get; set; }

    [Column("ano")]
    public int? Ano { get; set; }

    [Column("vin"), MaxLength(50)]
    public string? Vin { get; set; }

    [Column("tipo_combustivel"), MaxLength(30)]
    public string? TipoCombustivel { get; set; }

    [Column("cilindrada")]
    public int? Cilindrada { get; set; }

    [Column("potencia")]
    public int? Potencia { get; set; }

    [Column("lugares")]
    public int? Lugares { get; set; }

    [Column("peso")]
    public int? Peso { get; set; }

    [Column("proprietario_id")]
    public int? ProprietarioId { get; set; }

    [ForeignKey(nameof(ProprietarioId))]
    public ClienteCatalogo? Proprietario { get; set; }

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("criado_por")]
    public int CriadoPor { get; set; }

    [ForeignKey(nameof(CriadoPor))]
    public User? CriadoPorUtilizador { get; set; }

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}