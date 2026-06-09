using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("produtos")]
public class Produto
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("sku"), MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Column("nome"), MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("categoria"), MaxLength(100)]
    public string? Categoria { get; set; }

    [Column("fornecedor_id")]
    public int? FornecedorId { get; set; }

    [ForeignKey(nameof(FornecedorId))]
    public FornecedorCatalogo? Fornecedor { get; set; }

    [Column("preco_compra")]
    public decimal PrecoCompra { get; set; } = 0;

    [Column("preco_venda")]
    public decimal PrecoVenda { get; set; } = 0;

    [Column("iva")]
    public int Iva { get; set; } = 23;

    [Column("stock_atual")]
    public int StockAtual { get; set; } = 0;

    [Column("stock_minimo")]
    public int StockMinimo { get; set; } = 0;

    [Column("unidade_medida"), MaxLength(20)]
    public string UnidadeMedida { get; set; } = "un";

    [Column("localizacao"), MaxLength(50)]
    public string? Localizacao { get; set; }

    [Column("lote_obrigatorio")]
    public bool LoteObrigatorio { get; set; } = false;

    [Column("validade_obrigatoria")]
    public bool ValidadeObrigatoria { get; set; } = false;

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_por")]
    public int? CriadoPor { get; set; }

    [ForeignKey(nameof(CriadoPor))]
    public User? CriadoPorUtilizador { get; set; }

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Estoque>             Estoques      { get; set; } = [];
    public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = [];

    [Column("peso_unitario")]
    public decimal PesoUnitario { get; set; } = 0;

    [Column("volume_unitario")]
    public int VolumeUnitario { get; set; } = 0;
}
