using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

// ─── DTO de leitura (response) ────────────────────────────────────────────────
public class AtribuicaoResponseDto
{
    public int Id { get; set; }
    public string NumeroAtribuicao { get; set; } = string.Empty;
    public DateTime DataAtribuicao { get; set; }
    public string Status { get; set; } = string.Empty; // Pendente, EmProgresso, Concluida, Cancelada
    public string Prioridade { get; set; } = string.Empty; // Baixa, Media, Alta, Urgente
    
    // Dados da viagem/entrega
    public string? ClienteNome { get; set; }
    public string? ClienteContacto { get; set; }
    public string? EnderecoOrigem { get; set; }
    public string? EnderecoDestino { get; set; }
    public DateTime? DataPrevistaInicio { get; set; }
    public DateTime? DataPrevistaFim { get; set; }
    public string? Observacoes { get; set; }
    
    // Recursos atribuídos
    public int? MotoristaId { get; set; }
    public string? MotoristaNome { get; set; }
    public int? VeiculoId { get; set; }
    public string? VeiculoMatricula { get; set; }
    public string? VeiculoMarca { get; set; }
    public string? VeiculoModelo { get; set; }
    public int? TransportadoraId { get; set; }
    public string? TransportadoraNome { get; set; }
    public List<string> AjudanteNomes { get; set; } = [];
    
    // Estatísticas
    public int TotalEntregas { get; set; }
    public int EntregasRealizadas { get; set; }
    public decimal DistanciaTotalKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    
    // Entregas detalhadas
    public List<AtribuicaoEntregaDto>? Entregas { get; set; }
}

public class AtribuicaoEntregaDto
{
    public int Id { get; set; }
    public string? Destinatario { get; set; }
    public string? Endereco { get; set; }
    public string? Contacto { get; set; }
    public string? Observacoes { get; set; }
    public int Ordem { get; set; }
    public bool Realizada { get; set; }
}

// ─── DTO de criação ───────────────────────────────────────────────────────────
public class AtribuicaoCreateDto
{
    [Required(ErrorMessage = "Cliente é obrigatório.")]
    [MaxLength(200)]
    public string ClienteNome { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? ClienteContacto { get; set; }
    
    [MaxLength(300)]
    public string? EnderecoOrigem { get; set; }
    
    [Required(ErrorMessage = "Endereço de destino é obrigatório.")]
    [MaxLength(300)]
    public string? EnderecoDestino { get; set; }
    
    public DateTime? DataPrevistaInicio { get; set; }
    public DateTime? DataPrevistaFim { get; set; }
    
    [Required(ErrorMessage = "Prioridade é obrigatória.")]
    public string Prioridade { get; set; } = "Media";
    
    public string? Observacoes { get; set; }
    
    // Recursos
    public int? MotoristaId { get; set; }
    public int? VeiculoId { get; set; }
    public int? TransportadoraId { get; set; }
    public List<int> AjudanteIds { get; set; } = [];
    
    public decimal DistanciaTotalKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    
    // Entregas associadas
    public List<AtribuicaoEntregaCreateDto>? Entregas { get; set; }
}

public class AtribuicaoEntregaCreateDto
{
    public string? Destinatario { get; set; }
    public string? Endereco { get; set; }
    public string? Contacto { get; set; }
    public string? Observacoes { get; set; }
    public int Ordem { get; set; }
}

// ─── DTO de actualização ──────────────────────────────────────────────────────
public class AtribuicaoUpdateDto
{
    public string? Status { get; set; }
    public string? Prioridade { get; set; }
    public DateTime? DataPrevistaInicio { get; set; }
    public DateTime? DataPrevistaFim { get; set; }
    public string? Observacoes { get; set; }
    public int? MotoristaId { get; set; }
    public int? VeiculoId { get; set; }
    public int? TransportadoraId { get; set; }
    public List<int> AjudanteIds { get; set; } = [];
    public decimal? DistanciaTotalKm { get; set; }
    public decimal? TempoEstimadoHoras { get; set; }
    public List<AtribuicaoEntregaUpdateDto>? Entregas { get; set; }
}

public class AtribuicaoEntregaUpdateDto
{
    public int? Id { get; set; }
    public string? Destinatario { get; set; }
    public string? Endereco { get; set; }
    public string? Contacto { get; set; }
    public string? Observacoes { get; set; }
    public int? Ordem { get; set; }
    public bool? Realizada { get; set; }
}