using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

// ─── DTO de leitura (response) ────────────────────────────────────────────────
public class FechoViagemResponseDto
{
    public int Id { get; set; }
    public string NumeroFecho { get; set; } = string.Empty;
    public int AtribuicaoId { get; set; }
    public string? AtribuicaoNumero { get; set; }
    public string? ClienteNome { get; set; }
    public DateTime DataFecho { get; set; }
    public string Status { get; set; } = string.Empty; // Pendente, Processado, Cancelado
    
    // Tempos
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }
    public TimeSpan? TempoTotalReal { get; set; }
    public TimeSpan? TempoPlaneado { get; set; }
    public TimeSpan? DiferencaTempo { get; set; }
    
    // Consumos
    public decimal? CombustivelLitros { get; set; }
    public decimal? CombustivelCusto { get; set; }
    public decimal? PortagensCusto { get; set; }
    public decimal? OutrosCustos { get; set; }
    public string? CustosExtrasDescricao { get; set; }
    public decimal CustoTotal { get; set; }
    
    // Quilometragem
    public int? QuilometrosInicio { get; set; }
    public int? QuilometrosFim { get; set; }
    public int? QuilometrosPercorridos { get; set; }
    
    // Entregas
    public int TotalEntregas { get; set; }
    public int EntregasRealizadas { get; set; }
    public int EntregasNaoRealizadas { get; set; }
    public string? EntregasPendentesObs { get; set; }
    
    // Incidentes
    public bool TemIncidentes { get; set; }
    public string? IncidentesDescricao { get; set; }
    
    // Faturação
    public bool Faturado { get; set; }
    public int? FaturaId { get; set; }
    public string? FaturaNumero { get; set; }
    public string? Observacoes { get; set; }
    
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    
    public List<FechoViagemEntregaDto>? EntregasDetalhe { get; set; }
}

public class FechoViagemEntregaDto
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public string? Destinatario { get; set; }
    public string? Endereco { get; set; }
    public bool Realizada { get; set; }
    public DateTime? DataHoraRealizacao { get; set; }
    public string? Observacoes { get; set; }
}

// ─── DTO de criação ───────────────────────────────────────────────────────────
public class FechoViagemCreateDto
{
    [Required(ErrorMessage = "Atribuição é obrigatória.")]
    public int AtribuicaoId { get; set; }
    
    // Tempos reais
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }
    
    // Consumos
    [Range(0, double.MaxValue)]
    public decimal? CombustivelLitros { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? CombustivelCusto { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? PortagensCusto { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? OutrosCustos { get; set; }
    
    public string? CustosExtrasDescricao { get; set; }
    
    // Quilometragem
    [Range(0, int.MaxValue)]
    public int? QuilometrosInicio { get; set; }
    
    [Range(0, int.MaxValue)]
    public int? QuilometrosFim { get; set; }
    
    // Entregas não realizadas
    public List<int>? EntregasNaoRealizadasIds { get; set; }
    public string? EntregasPendentesObs { get; set; }
    
    // Incidentes
    public bool TemIncidentes { get; set; }
    public string? IncidentesDescricao { get; set; }
    
    public string? Observacoes { get; set; }
}

// ─── DTO de actualização ──────────────────────────────────────────────────────
public class FechoViagemUpdateDto
{
    public string? Status { get; set; }
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }
    public decimal? CombustivelLitros { get; set; }
    public decimal? CombustivelCusto { get; set; }
    public decimal? PortagensCusto { get; set; }
    public decimal? OutrosCustos { get; set; }
    public string? CustosExtrasDescricao { get; set; }
    public int? QuilometrosInicio { get; set; }
    public int? QuilometrosFim { get; set; }
    public List<int>? EntregasNaoRealizadasIds { get; set; }
    public string? EntregasPendentesObs { get; set; }
    public bool? TemIncidentes { get; set; }
    public string? IncidentesDescricao { get; set; }
    public string? Observacoes { get; set; }
    public bool? Faturado { get; set; }
}