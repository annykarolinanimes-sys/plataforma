using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

// ─── DTO de leitura (response) ────────────────────────────────────────────────
public class GuiaResponseDto
{
    public int Id { get; set; }
    public string NumeroGuia { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // Transporte, Remessa, Entrega
    public string Status { get; set; } = string.Empty; // Pendente, Impressa, Enviada, Cancelada
    public DateTime DataEmissao { get; set; }
    
    // Dados da viagem/atribuição
    public int? AtribuicaoId { get; set; }
    public string? AtribuicaoNumero { get; set; }
    
    // Cliente
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public string? ClienteNif { get; set; }
    public string? ClienteMorada { get; set; }
    public string? ClienteContacto { get; set; }
    
    // Transportadora
    public int? TransportadoraId { get; set; }
    public string? TransportadoraNome { get; set; }
    public string? TransportadoraNif { get; set; }
    
    // Endereços
    public string? EnderecoOrigem { get; set; }
    public string? EnderecoDestino { get; set; }
    
    // Dados da carga
    public int TotalItens { get; set; }
    public decimal PesoTotalKg { get; set; }
    public int VolumeTotalM3 { get; set; }
    public int TotalVolumes { get; set; }
    
    // Datas
    public DateTime? DataPrevistaEntrega { get; set; }
    public DateTime? DataEntregaReal { get; set; }
    
    // Observações
    public string? Observacoes { get; set; }
    public string? InstrucoesEspeciais { get; set; }
    
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    
    public List<GuiaItemResponseDto>? Itens { get; set; }
}

public class GuiaItemResponseDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string? ProdutoSku { get; set; }
    public string? ProdutoNome { get; set; }
    public int Quantidade { get; set; }
    public decimal PesoUnitario { get; set; }
    public decimal PesoTotal { get; set; }
    public int VolumeUnitario { get; set; }
    public int VolumeTotal { get; set; }
    public string? Lote { get; set; }
    public string? Observacoes { get; set; }
}

// ─── DTO de criação ───────────────────────────────────────────────────────────
public class GuiaCreateDto
{
    [Required(ErrorMessage = "Tipo de guia é obrigatório.")]
    public string Tipo { get; set; } = "Transporte";
    
    public int? AtribuicaoId { get; set; }
    public int? ClienteId { get; set; }
    public int? TransportadoraId { get; set; }
    
    [MaxLength(300)]
    public string? EnderecoOrigem { get; set; }
    
    [MaxLength(300)]
    public string? EnderecoDestino { get; set; }
    
    public DateTime? DataPrevistaEntrega { get; set; }
    
    [MaxLength(500)]
    public string? Observacoes { get; set; }
    
    [MaxLength(500)]
    public string? InstrucoesEspeciais { get; set; }
    
    [MinLength(1, ErrorMessage = "Adicione pelo menos um item.")]
    public List<GuiaItemCreateDto> Itens { get; set; } = [];
}

public class GuiaItemCreateDto
{
    [Required]
    public int ProdutoId { get; set; }
    
    [Range(1, int.MaxValue)]
    public int Quantidade { get; set; }
    
    public string? Lote { get; set; }
    public string? Observacoes { get; set; }
}

// ─── DTO de actualização ──────────────────────────────────────────────────────
public class GuiaUpdateDto
{
    public string? Status { get; set; }
    public DateTime? DataPrevistaEntrega { get; set; }
    public DateTime? DataEntregaReal { get; set; }
    public string? Observacoes { get; set; }
    public string? InstrucoesEspeciais { get; set; }
    public List<GuiaItemUpdateDto>? Itens { get; set; }
}

public class GuiaItemUpdateDto
{
    public int? Id { get; set; }
    public int? ProdutoId { get; set; }
    public int? Quantidade { get; set; }
    public string? Lote { get; set; }
    public string? Observacoes { get; set; }
}