using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("incidentes")]
public class Incidente
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_incidente"), MaxLength(50)]
    public string NumeroIncidente { get; set; } = string.Empty;

    [Column("data_ocorrencia")]
    public DateTime DataOcorrencia { get; set; } = DateTime.UtcNow;

    [Column("tipo"), MaxLength(50)]
    public string Tipo { get; set; } = string.Empty;

    [Column("gravidade"), MaxLength(20)]
    public string Gravidade { get; set; } = "Media";

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Aberto";

    [Column("titulo"), MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Column("descricao")]
    public string? Descricao { get; set; }

    // Entidades associadas
    [Column("viagem_id")]
    public int? ViagemId { get; set; }

    [ForeignKey(nameof(ViagemId))]
    public GestaoViagem? Viagem { get; set; }

    [Column("veiculo_id")]
    public int? VeiculoId { get; set; }

    [ForeignKey(nameof(VeiculoId))]
    public Veiculo? Veiculo { get; set; }

    [Column("cliente_id")]
    public int? ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public ClienteCatalogo? Cliente { get; set; }

    [Column("atribuicao_id")]
    public int? AtribuicaoId { get; set; }

    [ForeignKey(nameof(AtribuicaoId))]
    public Atribuicao? Atribuicao { get; set; }

    // Dados do incidente
    [Column("data_resolucao")]
    public DateTime? DataResolucao { get; set; }

    [Column("causa")]
    public string? Causa { get; set; }

    [Column("acao_corretiva")]
    public string? AcaoCorretiva { get; set; }

    [Column("responsavel_resolucao"), MaxLength(200)]
    public string? ResponsavelResolucao { get; set; }

    [Column("custo_associado")]
    public decimal? CustoAssociado { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    // Auditoria
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;
}