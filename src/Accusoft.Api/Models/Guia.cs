using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("guias")]
public class Guia
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_guia"), MaxLength(50)]
    public string NumeroGuia { get; set; } = string.Empty;

    [Column("tipo"), MaxLength(50)]
    public string Tipo { get; set; } = "Transporte";

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Pendente";

    [Column("data_emissao")]
    public DateTime DataEmissao { get; set; } = DateTime.UtcNow;

    // Relacionamentos
    [Column("atribuicao_id")]
    public int? AtribuicaoId { get; set; }

    [ForeignKey(nameof(AtribuicaoId))]
    public Atribuicao? Atribuicao { get; set; }

    [Column("cliente_id")]
    public int? ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public ClienteCatalogo? Cliente { get; set; }

    [Column("transportadora_id")]
    public int? TransportadoraId { get; set; }

    [ForeignKey(nameof(TransportadoraId))]
    public TransportadoraCatalogo? Transportadora { get; set; }

    // Endereços
    [Column("endereco_origem"), MaxLength(300)]
    public string? EnderecoOrigem { get; set; }

    [Column("endereco_destino"), MaxLength(300)]
    public string? EnderecoDestino { get; set; }

    // Dados da carga
    [Column("total_itens")]
    public int TotalItens { get; set; }

    [Column("peso_total_kg")]
    public decimal PesoTotalKg { get; set; }

    [Column("volume_total_m3")]
    public int VolumeTotalM3 { get; set; }

    [Column("total_volumes")]
    public int TotalVolumes { get; set; }

    // Datas
    [Column("data_prevista_entrega")]
    public DateTime? DataPrevistaEntrega { get; set; }

    [Column("data_entrega_real")]
    public DateTime? DataEntregaReal { get; set; }

    // Observações
    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("instrucoes_especiais")]
    public string? InstrucoesEspeciais { get; set; }

    // Auditoria
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<GuiaItem> Itens { get; set; } = [];
}

[Table("guia_itens")]
public class GuiaItem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("guia_id")]
    public int GuiaId { get; set; }

    [ForeignKey(nameof(GuiaId))]
    public Guia Guia { get; set; } = null!;

    [Column("produto_id")]
    public int ProdutoId { get; set; }

    [ForeignKey(nameof(ProdutoId))]
    public Produto Produto { get; set; } = null!;

    [Column("quantidade")]
    public int Quantidade { get; set; }

    [Column("peso_unitario")]
    public decimal PesoUnitario { get; set; }

    [Column("peso_total")]
    public decimal PesoTotal { get; set; }

    [Column("volume_unitario")]
    public int VolumeUnitario { get; set; }

    [Column("volume_total")]
    public int VolumeTotal { get; set; }

    [Column("lote"), MaxLength(100)]
    public string? Lote { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }
}