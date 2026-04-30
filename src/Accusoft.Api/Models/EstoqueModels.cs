using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("estoque")]
public class Estoque
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("produto_id")]
    public int ProdutoId { get; set; }

    [ForeignKey(nameof(ProdutoId))]
    public Produto Produto { get; set; } = null!;

    [Column("armazem_id")]
    public int? ArmazemId { get; set; }

    [ForeignKey(nameof(ArmazemId))]
    public Armazem? Armazem { get; set; }

    [Column("localizacao"), MaxLength(100)]
    public string? Localizacao { get; set; }

    [Column("lote"), MaxLength(100)]
    public string? Lote { get; set; }

    [Column("validade")]
    public DateOnly? Validade { get; set; }

    [Column("quantidade")]
    public int Quantidade { get; set; } = 0;

    [Column("quantidade_reservada")]
    public int QuantidadeReservada { get; set; } = 0;

    [Column("quantidade_picking")]
    public int QuantidadePicking { get; set; } = 0;

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "em-estoque";

    [Column("ultima_movimentacao")]
    public DateTimeOffset UltimaMovimentacao { get; set; } = DateTimeOffset.UtcNow;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = [];
}

[Table("movimentacoes_estoque")]
public class MovimentacaoEstoque
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("produto_id")]
    public int ProdutoId { get; set; }

    [ForeignKey(nameof(ProdutoId))]
    public Produto Produto { get; set; } = null!;

    [Column("tipo")]
    public MovimentacaoTipo Tipo { get; set; }

    [Column("quantidade")]
    public int Quantidade { get; set; }

    [Column("origem_local"), MaxLength(100)]
    public string? OrigemLocal { get; set; }

    [Column("destino_local"), MaxLength(100)]
    public string? DestinoLocal { get; set; }

    [Column("armazem_id")]
    public int? ArmazemId { get; set; }

    [ForeignKey(nameof(ArmazemId))]
    public Armazem? Armazem { get; set; }

    [Column("usuario_id")]
    public int? UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User? Usuario { get; set; }

    [Column("observacao")]
    public string? Observacao { get; set; }

    [Column("data_mov")]
    public DateTimeOffset DataMov { get; set; } = DateTimeOffset.UtcNow;
}
