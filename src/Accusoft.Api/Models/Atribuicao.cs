using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("atribuicoes")]
public class Atribuicao
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("numero_atribuicao"), MaxLength(50)]
    public string NumeroAtribuicao { get; set; } = string.Empty;

    [Column("data_atribuicao")]
    public DateTime DataAtribuicao { get; set; } = DateTime.UtcNow;

    [Column("status"), MaxLength(50)]
    public string Status { get; set; } = "Pendente";

    [Column("prioridade"), MaxLength(20)]
    public string Prioridade { get; set; } = "Media";

    // Dados da viagem
    [Column("cliente_nome"), MaxLength(200)]
    public string? ClienteNome { get; set; }

    [Column("cliente_contacto"), MaxLength(100)]
    public string? ClienteContacto { get; set; }

    [Column("endereco_origem"), MaxLength(300)]
    public string? EnderecoOrigem { get; set; }

    [Column("endereco_destino"), MaxLength(300)]
    public string? EnderecoDestino { get; set; }

    [Column("data_prevista_inicio")]
    public DateTime? DataPrevistaInicio { get; set; }

    [Column("data_prevista_fim")]
    public DateTime? DataPrevistaFim { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    // Recursos atribuídos
    [Column("motorista_id")]
    public int? MotoristaId { get; set; }

    [ForeignKey(nameof(MotoristaId))]
    public User? Motorista { get; set; }

    [Column("veiculo_id")]
    public int? VeiculoId { get; set; }

    [ForeignKey(nameof(VeiculoId))]
    public Veiculo? Veiculo { get; set; }

    [Column("transportadora_id")]
    public int? TransportadoraId { get; set; }

    [ForeignKey(nameof(TransportadoraId))]
    public TransportadoraCatalogo? Transportadora { get; set; }
    
    // Dados da viagem
    [Column("distancia_total_km")]
    public decimal DistanciaTotalKm { get; set; }

    [Column("tempo_estimado_horas")]
    public decimal? TempoEstimadoHoras { get; set; }

    // Auditoria
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public User Usuario { get; set; } = null!;

    [Column("criado_em")]
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    [Column("atualizado_em")]
    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.UtcNow;

    // Relacionamentos
    public ICollection<AtribuicaoEntrega> Entregas { get; set; } = [];
}

[Table("atribuicao_entregas")]
public class AtribuicaoEntrega
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("atribuicao_id")]
    public int AtribuicaoId { get; set; }

    [ForeignKey(nameof(AtribuicaoId))]
    public Atribuicao Atribuicao { get; set; } = null!;

    [Column("destinatario"), MaxLength(200)]
    public string? Destinatario { get; set; }

    [Column("endereco"), MaxLength(300)]
    public string? Endereco { get; set; }

    [Column("contacto"), MaxLength(100)]
    public string? Contacto { get; set; }

    [Column("observacoes")]
    public string? Observacoes { get; set; }

    [Column("ordem")]
    public int Ordem { get; set; }

    [Column("realizada")]
    public bool Realizada { get; set; } = false;
}