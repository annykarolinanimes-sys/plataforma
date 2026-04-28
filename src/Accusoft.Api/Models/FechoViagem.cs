using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("fechos_viagem")]
public class FechoViagem
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_fecho"), MaxLength(50)]
    public string NumeroFecho { get; set; } = string.Empty;

    [Column("atribuicao_id")]
    public int AtribuicaoId { get; set; }

    [ForeignKey(nameof(AtribuicaoId))]
    public Atribuicao? Atribuicao { get; set; }

    [Column("data_fecho")]
    public DateTime DataFecho { get; set; } = DateTime.UtcNow;

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Pendente";

    // Tempos reais
    [Column("data_inicio_real")]
    public DateTime? DataInicioReal { get; set; }

    [Column("data_fim_real")]
    public DateTime? DataFimReal { get; set; }

    // Consumos
    [Column("combustivel_litros")]
    public decimal? CombustivelLitros { get; set; }

    [Column("combustivel_custo")]
    public decimal? CombustivelCusto { get; set; }

    [Column("portagens_custo")]
    public decimal? PortagensCusto { get; set; }

    [Column("outros_custos")]
    public decimal? OutrosCustos { get; set; }

    [Column("custos_extras_descricao")]
    public string? CustosExtrasDescricao { get; set; }

    [Column("custo_total")]
    public decimal CustoTotal { get; set; }

    // Quilometragem
    [Column("quilometros_inicio")]
    public int? QuilometrosInicio { get; set; }

    [Column("quilometros_fim")]
    public int? QuilometrosFim { get; set; }

    [Column("quilometros_percorridos")]
    public int? QuilometrosPercorridos { get; set; }

    // Entregas
    [Column("entregas_nao_realizadas_ids")]
    public string? EntregasNaoRealizadasIds { get; set; } // JSON array

    [Column("entregas_pendentes_obs")]
    public string? EntregasPendentesObs { get; set; }

    // Incidentes
    [Column("tem_incidentes")]
    public bool TemIncidentes { get; set; }

    [Column("incidentes_descricao")]
    public string? IncidentesDescricao { get; set; }

    // Faturação
    [Column("faturado")]
    public bool Faturado { get; set; }

    [Column("fatura_id")]
    public int? FaturaId { get; set; }

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