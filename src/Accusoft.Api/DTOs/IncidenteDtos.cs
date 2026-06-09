using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;


public record IncidenteAnexoDto(
    int    Id,
    string NomeOriginal,
    string MimeType,
    long   TamanhoBytes,
    string TamanhoFormatado,
    DateTimeOffset CriadoEm
);

public class IncidenteResponseDto
{
    public int Id { get; set; }
    public string NumeroIncidente { get; set; } = string.Empty;
    public DateTime DataOcorrencia { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Gravidade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public int? ViagemId { get; set; }
    public string? ViagemNumero { get; set; }
    public int? VeiculoId { get; set; }
    public string? VeiculoMatricula { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public int? AtribuicaoId { get; set; }
    public string? AtribuicaoNumero { get; set; }

    public DateTime? DataResolucao { get; set; }
    public string? Causa { get; set; }
    public string? AcaoCorretiva { get; set; }
    public string? ResponsavelResolucao { get; set; }
    public decimal? CustoAssociado { get; set; }
    public string? Observacoes { get; set; }

    public int TotalAnexos { get; set; }
    public List<IncidenteAnexoDto> Anexos { get; set; } = [];  

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}


public class IncidenteCreateDto
{
    [Required(ErrorMessage = "Tipo de incidente é obrigatório.")]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gravidade é obrigatória.")]
    public string Gravidade { get; set; } = "Media";

    [Required(ErrorMessage = "Título é obrigatório.")]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descricao { get; set; }

    public DateTime? DataOcorrencia { get; set; }

    public int? ViagemId { get; set; }
    public int? VeiculoId { get; set; }
    public int? ClienteId { get; set; }
    public int? AtribuicaoId { get; set; }

    public string? Causa { get; set; }
    public string? AcaoCorretiva { get; set; }
    public string? ResponsavelResolucao { get; set; }
    public decimal? CustoAssociado { get; set; }
    public string? Observacoes { get; set; }
}


public class IncidenteUpdateDto
{
    public string? Status { get; set; }
    public string? Gravidade { get; set; }
    public string? Descricao { get; set; }
    public string? Causa { get; set; }
    public string? AcaoCorretiva { get; set; }
    public string? ResponsavelResolucao { get; set; }
    public decimal? CustoAssociado { get; set; }
    public string? Observacoes { get; set; }
    public DateTime? DataResolucao { get; set; }
}

public class ResolverIncidenteDto
{
    [Required(ErrorMessage = "Ação corretiva é obrigatória.")]
    public string AcaoCorretiva { get; set; } = string.Empty;

    public string? ResponsavelResolucao { get; set; }
    public decimal? CustoAssociado { get; set; }
    public string? Observacoes { get; set; }
}