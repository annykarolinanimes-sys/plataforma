using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("faturas")]
public class Invoice
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_fatura"), MaxLength(50)]
    public string NumeroFatura { get; set; } = string.Empty;

    [Column("cliente_nome"), MaxLength(200)]
    public string ClienteNome { get; set; } = string.Empty;

    [Column("cliente_contacto"), MaxLength(100)]
    public string ClienteContacto { get; set; } = string.Empty;

    [Column("cliente_email"), MaxLength(200)]
    public string? ClienteEmail { get; set; }

    [Column("cliente_morada"), MaxLength(300)]
    public string? ClienteMorada { get; set; }

    [Column("cliente_nif"), MaxLength(20)]
    public string? ClienteNif { get; set; }

    [Column("cliente_id")]
    public int? ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public ClienteCatalogo? Cliente { get; set; }

    [Column("viagem_id")]
    public int? ViagemId { get; set; }

    [ForeignKey(nameof(ViagemId))]
    public GestaoViagem? Viagem { get; set; }

    [Column("pdf_url"), MaxLength(500)]
    public string? PdfUrl { get; set; }

    [Column("data_doc")]
    public DateOnly DataDoc { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Column("estado"), MaxLength(50)]
    public string Estado { get; set; } = "Pendente";

    [Column("valor_total")]
    public decimal ValorTotal { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("quem_executou"), MaxLength(200)]
    public string? QuemExecutou { get; set; }

    [Column("horas_trabalho")]
    public decimal? HorasTrabalho { get; set; }

    [Column("material_utilizado")]
    public string? MaterialUtilizado { get; set; }

    public ICollection<InvoiceItem> Itens { get; set; } = [];
}

[Table("fatura_itens")]
public class InvoiceItem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("fatura_id")]
    public int FaturaId { get; set; }

    [ForeignKey(nameof(FaturaId))]
    public Invoice Fatura { get; set; } = null!;

    [Column("marca"), MaxLength(100)]
    public string Marca { get; set; } = string.Empty;

    [Column("modelo"), MaxLength(100)]
    public string Modelo { get; set; } = string.Empty;

    [Column("cor"), MaxLength(50)]
    public string Cor { get; set; } = string.Empty;

    [Column("matricula"), MaxLength(20)]
    public string Matricula { get; set; } = string.Empty;

    [Column("quantidade")]
    public int Quantidade { get; set; } = 1;

    [Column("preco_unitario")]
    public decimal PrecoUnitario { get; set; }

    [Column("subtotal")]
    public decimal Subtotal { get; set; }
}