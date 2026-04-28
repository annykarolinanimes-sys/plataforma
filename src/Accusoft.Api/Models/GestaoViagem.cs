using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("gestao_viagens")]
public class GestaoViagem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_viagem"), MaxLength(50)]
    public string NumeroViagem { get; set; } = string.Empty;

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Planeada";

    [Column("prioridade"), MaxLength(20)]
    public string Prioridade { get; set; } = "Media";

    [Column("data_criacao")]
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    [Column("data_inicio_planeada")]
    public DateTime? DataInicioPlaneada { get; set; }

    [Column("data_fim_planeada")]
    public DateTime? DataFimPlaneada { get; set; }

    [Column("data_inicio_real")]
    public DateTime? DataInicioReal { get; set; }

    [Column("data_fim_real")]
    public DateTime? DataFimReal { get; set; }

    // Relacionamentos
    [Column("rota_id")]
    public int? RotaId { get; set; }

    [ForeignKey(nameof(RotaId))]
    public RotaCatalogo? Rota { get; set; }

    [Column("veiculo_id")]
    public int? VeiculoId { get; set; }

    [ForeignKey(nameof(VeiculoId))]
    public Veiculo? Veiculo { get; set; }

    [Column("motorista_id")]
    public int? MotoristaId { get; set; }

    [ForeignKey(nameof(MotoristaId))]
    public User? Motorista { get; set; }

    [Column("transportadora_id")]
    public int? TransportadoraId { get; set; }

    [ForeignKey(nameof(TransportadoraId))]
    public TransportadoraCatalogo? Transportadora { get; set; }

    // Carga
    [Column("carga_descricao"), MaxLength(500)]
    public string? CargaDescricao { get; set; }

    [Column("carga_peso")]
    public decimal CargaPeso { get; set; }

    [Column("carga_volume")]
    public int CargaVolume { get; set; }

    [Column("carga_observacoes"), MaxLength(500)]
    public string? CargaObservacoes { get; set; }

    // Estatísticas
    [Column("distancia_total_km")]
    public decimal DistanciaTotalKm { get; set; }

    [Column("distancia_percorrida_km")]
    public decimal DistanciaPercorridaKm { get; set; }

    [Column("tempo_estimado_horas")]
    public decimal? TempoEstimadoHoras { get; set; }

    // Observações
    [Column("observacoes"), MaxLength(1000)]
    public string? Observacoes { get; set; }

    // Auditoria
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}