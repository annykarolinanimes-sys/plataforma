using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("rececoes")]
public class Recepcao
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_recepcao"), MaxLength(50)]
    public string NumeroRecepcao { get; set; } = string.Empty;

    [Column("fornecedor_id")]
    public int FornecedorId { get; set; }

    [ForeignKey(nameof(FornecedorId))]
    public FornecedorCatalogo Fornecedor { get; set; } = null!;

    [Column("tipo_entrada"), MaxLength(50)]
    public string TipoEntrada { get; set; } = "Fornecedor";

    [Column("data_recepcao")]
    public DateTime DataRecepcao { get; set; } = DateTime.UtcNow;

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Pendente";

    [Column("prioridade"), MaxLength(20)]
    public string Prioridade { get; set; } = "Media";

    [Column("documento_referencia"), MaxLength(100)]
    public string? DocumentoReferencia { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User? Usuario { get; set; }

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecepcaoItem> Itens { get; set; } = [];
}

[Table("recepcao_itens")]
public class RecepcaoItem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("recepcao_id")]
    public int RecepcaoId { get; set; }

    [ForeignKey(nameof(RecepcaoId))]
    public Recepcao Recepcao { get; set; } = null!;

    [Column("produto_id")]
    public int ProdutoId { get; set; }

    [ForeignKey(nameof(ProdutoId))]
    public Produto Produto { get; set; } = null!;

    [Column("quantidade_esperada")]
    public int QuantidadeEsperada { get; set; }

    [Column("quantidade_recebida")]
    public int QuantidadeRecebida { get; set; }

    [Column("quantidade_rejeitada")]
    public int QuantidadeRejeitada { get; set; }

    [Column("lote"), MaxLength(100)]
    public string? Lote { get; set; }

    [Column("validade")]
    public DateOnly? Validade { get; set; }

    [Column("localizacao"), MaxLength(50)]
    public string? Localizacao { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("conformidade")]
    public bool Conformidade { get; set; } = true;

    
}