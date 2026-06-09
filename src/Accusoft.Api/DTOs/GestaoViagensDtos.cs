using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

public class GestaoViagemResponseDto
{
    public int Id { get; set; }
    public string NumeroViagem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; 
    public string Prioridade { get; set; } = string.Empty; 
    public DateTime DataCriacao { get; set; }
    public DateTime? DataInicioPlaneada { get; set; }
    public DateTime? DataFimPlaneada { get; set; }
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }
    
    public int? VeiculoId { get; set; }
    public string? VeiculoMatricula { get; set; }
    public string? VeiculoMarca { get; set; }
    public string? VeiculoModelo { get; set; }
    public int? MotoristaId { get; set; }
    public string? MotoristaNome { get; set; }
    public int? TransportadoraId { get; set; }
    public string? TransportadoraNome { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public string? Origem { get; set; }
    public string? Destino { get; set; }
    public decimal PrecoPorKm { get; set; }
    public string? CargaDescricao { get; set; }
    public decimal CargaPeso { get; set; }
    public int CargaVolume { get; set; }
    public string? CargaObservacoes { get; set; }
    public int TotalEntregas { get; set; }
    public int EntregasConcluidas { get; set; }
    public int EntregasPendentes { get; set; }
    public decimal DistanciaTotalKm { get; set; }
    public decimal DistanciaPercorridaKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    public decimal? TempoRealHoras { get; set; }
    public decimal? AtrasoHoras { get; set; }
    
    public decimal ProgressoPercentual { get; set; }

    public string? Observacoes { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}
public class GestaoViagemCreateDto
{
    [Required(ErrorMessage = "Prioridade é obrigatória.")]
    public string Prioridade { get; set; } = "Media";
    
    public DateTime? DataInicioPlaneada { get; set; }
    public DateTime? DataFimPlaneada { get; set; }
    
    public int? VeiculoId { get; set; }
    public int? MotoristaId { get; set; }
    public int? TransportadoraId { get; set; }
    public int? ClienteId { get; set; }
    public string? Origem { get; set; }
    public string? Destino { get; set; }
    public decimal PrecoPorKm { get; set; }
    
    [MaxLength(500)]
    public string? CargaDescricao { get; set; }
    
    [Range(0, 100000)]
    public decimal CargaPeso { get; set; }
    
    [Range(0, 10000)]
    public int CargaVolume { get; set; }
    
    [MaxLength(500)]
    public string? CargaObservacoes { get; set; }
    
    public decimal DistanciaTotalKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    
    [MaxLength(1000)]
    public string? Observacoes { get; set; }
}

public class GestaoViagemUpdateDto
{
    public string? Status { get; set; }
    public string? Prioridade { get; set; }
    public DateTime? DataInicioPlaneada { get; set; }
    public DateTime? DataFimPlaneada { get; set; }
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }
    public int? VeiculoId { get; set; }
    public int? MotoristaId { get; set; }
    public int? TransportadoraId { get; set; }
    public int? ClienteId { get; set; }
    public string? Origem { get; set; }
    public string? Destino { get; set; }
    public decimal? PrecoPorKm { get; set; }
    public string? CargaDescricao { get; set; }
    public decimal? CargaPeso { get; set; }
    public int? CargaVolume { get; set; }
    public string? CargaObservacoes { get; set; }
    public decimal? DistanciaTotalKm { get; set; }
    public decimal? DistanciaPercorridaKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    public string? Observacoes { get; set; }
}