using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

public class RecepcaoResponseDto
{
    public int Id { get; set; }
    public string NumeroRecepcao { get; set; } = string.Empty;
    public int FornecedorId { get; set; }
    public string Fornecedor { get; set; } = string.Empty;
    public string TipoEntrada { get; set; } = string.Empty;
    public DateTime DataRecepcao { get; set; }
    public string Status { get; set; } = string.Empty; 
    public string Prioridade { get; set; } = string.Empty; 
    public string? DocumentoReferencia { get; set; }
    public int TotalItens { get; set; }
    public int TotalUnidades { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    public List<RecepcaoItemResponseDto> Itens { get; set; } = [];
}

public class RecepcaoItemResponseDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProdutoNome { get; set; } = string.Empty;
    public int QuantidadeEsperada { get; set; }
    public int QuantidadeRecebida { get; set; }
    public int QuantidadeRejeitada { get; set; }
    public int QuantidadeAceite => QuantidadeRecebida - QuantidadeRejeitada;
    public string? Lote { get; set; }
    public DateOnly? Validade { get; set; }
    public string? Localizacao { get; set; }
    public string? Observacoes { get; set; }
    public bool Conformidade { get; set; }
}

public class RecepcaoCreateDto
{
    [Required(ErrorMessage = "Fornecedor é obrigatório.")]
    public int FornecedorId { get; set; }

    [Required(ErrorMessage = "Tipo de entrada é obrigatório.")]
    public string TipoEntrada { get; set; } = "Fornecedor";

    [Required(ErrorMessage = "Prioridade é obrigatória.")]
    public string Prioridade { get; set; } = "Media";

    [MaxLength(100)]
    public string? DocumentoReferencia { get; set; }

    [MinLength(1, ErrorMessage = "Adicione pelo menos um item.")]
    public List<RecepcaoItemCreateDto> Itens { get; set; } = [];
}

public class RecepcaoItemCreateDto
{
    [Required(ErrorMessage = "Produto é obrigatório.")]
    public int ProdutoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade esperada deve ser maior que zero.")]
    public int QuantidadeEsperada { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantidade recebida não pode ser negativa.")]
    public int QuantidadeRecebida { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantidade rejeitada não pode ser negativa.")]
    public int QuantidadeRejeitada { get; set; }

    [MaxLength(100)]
    public string? Lote { get; set; }

    public DateOnly? Validade { get; set; }

    [MaxLength(50)]
    public string? Localizacao { get; set; }

    public string? Observacoes { get; set; }
}

public class RecepcaoUpdateDto
{
    public int? FornecedorId { get; set; }
    public string? Status { get; set; }
    public string? Prioridade { get; set; }
    public string? DocumentoReferencia { get; set; }
    public List<RecepcaoItemUpdateDto>? Itens { get; set; }
}

public class RecepcaoItemUpdateDto
{
    public int? Id { get; set; }
    public int? ProdutoId { get; set; }
    public int? QuantidadeEsperada { get; set; }
    public int? QuantidadeRecebida { get; set; }
    public int? QuantidadeRejeitada { get; set; }
    public string? Lote { get; set; }
    public DateOnly? Validade { get; set; }
    public string? Localizacao { get; set; }
    public string? Observacoes { get; set; }
}

