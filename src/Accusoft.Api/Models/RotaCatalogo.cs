using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("rotas")]
public class RotaCatalogo
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("codigo"), MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("nome"), MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("origem"), MaxLength(200)]
    public string? Origem { get; set; }

    [Column("destino"), MaxLength(200)]
    public string? Destino { get; set; }

    [Column("distancia_km")]
    public decimal? DistanciaKm { get; set; }

    [Column("tempo_estimado_min")]
    public int? TempoEstimadoMin { get; set; }

    [Column("transportadora_id")]
    public int? TransportadoraId { get; set; }

    [ForeignKey(nameof(TransportadoraId))]
    public TransportadoraCatalogo? Transportadora { get; set; }

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