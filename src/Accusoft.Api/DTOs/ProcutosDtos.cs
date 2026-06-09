using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

public class ProdutoResponseDto
{
    public int     Id                  { get; set; }
    public string  Sku                 { get; set; } = string.Empty;
    public string  Nome                { get; set; } = string.Empty;
    public string? Descricao           { get; set; }
    public string? Categoria           { get; set; }
    public int?    FornecedorId        { get; set; }
    public string? FornecedorCodigo    { get; set; }
    public string? FornecedorNome      { get; set; }   
    public decimal PrecoCompra         { get; set; }
    public decimal PrecoVenda          { get; set; }
    public int     Iva                 { get; set; }
    public int     StockAtual          { get; set; }   
    public int     StockMinimo         { get; set; }
    public string  UnidadeMedida       { get; set; } = "un";
    public string? Localizacao         { get; set; }
    public bool    LoteObrigatorio     { get; set; }
    public bool    ValidadeObrigatoria { get; set; }
    public bool    Ativo               { get; set; }
    public DateTimeOffset CriadoEm    { get; set; }
    public DateTimeOffset AtualizadoEm{ get; set; }
}

public class ProdutoCreateDto
{
    [Required(ErrorMessage = "Nome do produto é obrigatório.")]
    [MaxLength(300, ErrorMessage = "Nome não pode exceder 300 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    public string? Descricao  { get; set; }

    [MaxLength(100)]
    public string? Categoria  { get; set; }

    public int? FornecedorId  { get; set; }

    [MaxLength(50)]
    public string? FornecedorCodigo { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Preço de compra não pode ser negativo.")]
    public decimal PrecoCompra { get; set; } = 0;

    [Range(0, double.MaxValue, ErrorMessage = "Preço de venda não pode ser negativo.")]
    public decimal PrecoVenda  { get; set; } = 0;

    [Range(0, 100, ErrorMessage = "IVA deve estar entre 0 e 100.")]
    public int Iva { get; set; } = 23;

    [Range(0, int.MaxValue, ErrorMessage = "Stock inicial não pode ser negativo.")]
    public int StockInicial { get; set; } = 0;      

    [Range(0, int.MaxValue, ErrorMessage = "Stock mínimo não pode ser negativo.")]
    public int StockMinimo { get; set; } = 0;

    [MaxLength(20)]
    public string UnidadeMedida { get; set; } = "un";

    [MaxLength(50)]
    public string? Localizacao         { get; set; }

    public bool LoteObrigatorio     { get; set; } = false;
    public bool ValidadeObrigatoria { get; set; } = false;
}

public class ProdutoUpdateDto
{
    [Required(ErrorMessage = "Nome do produto é obrigatório.")]
    [MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    public string? Descricao  { get; set; }

    [MaxLength(100)]
    public string? Categoria  { get; set; }

    public int? FornecedorId  { get; set; }

    [MaxLength(50)]
    public string? FornecedorCodigo { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecoCompra { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecoVenda  { get; set; }

    [Range(0, 100)]
    public int Iva { get; set; } = 23;

    [Range(0, int.MaxValue)]
    public int StockMinimo { get; set; }

    [MaxLength(20)]
    public string UnidadeMedida { get; set; } = "un";

    [MaxLength(50)]
    public string? Localizacao         { get; set; }

    public bool LoteObrigatorio     { get; set; }
    public bool ValidadeObrigatoria { get; set; }
    public bool Ativo               { get; set; } = true;
}

public class PagedResult<T>
{
    public IEnumerable<T> Items      { get; set; } = [];
    public int            Total      { get; set; }
    public int            Page       { get; set; }
    public int            PageSize   { get; set; }
    public int            TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
