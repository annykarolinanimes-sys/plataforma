using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.Controllers;

public record MotoristaResponseDto(
    int Id,
    string Nome,
    string Telefone,
    string CartaConducao,
    string? Nif,
    DateOnly? ValidadeCartaConducao,
    string TransportadoraId,
    bool Ativo,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm
);

public class MotoristaCreateDto
{
    [Required(ErrorMessage = "Nome do motorista é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefone é obrigatório.")]
    [MaxLength(30, ErrorMessage = "Telefone não pode exceder 30 caracteres.")]
    [Phone(ErrorMessage = "Formato de telefone inválido.")]
    public string Telefone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Carta de condução é obrigatória.")]
    [MaxLength(50, ErrorMessage = "Carta de condução não pode exceder 50 caracteres.")]
    public string CartaConducao { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$",
        ErrorMessage = "NIF inválido (5-20 caracteres alfanuméricos).")]
    public string? Nif { get; set; }

    [Required(ErrorMessage = "Data de validade da carta é obrigatória.")]
    [DataType(DataType.Date, ErrorMessage = "Data de validade inválida.")]
    public DateOnly? ValidadeCartaConducao { get; set; }

    [Required(ErrorMessage = "Transportadora é obrigatória.")]
    [MaxLength(50, ErrorMessage = "ID da transportadora não pode exceder 50 caracteres.")]
    public string TransportadoraId { get; set; } = string.Empty;
}

public class MotoristaUpdateDto
{
    [Required(ErrorMessage = "Nome do motorista é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefone é obrigatório.")]
    [MaxLength(30, ErrorMessage = "Telefone não pode exceder 30 caracteres.")]
    [Phone(ErrorMessage = "Formato de telefone inválido.")]
    public string Telefone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Carta de condução é obrigatória.")]
    [MaxLength(50, ErrorMessage = "Carta de condução não pode exceder 50 caracteres.")]
    public string CartaConducao { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$",
        ErrorMessage = "NIF inválido (5-20 caracteres alfanuméricos).")]
    public string? Nif { get; set; }

    [Required(ErrorMessage = "Data de validade da carta é obrigatória.")]
    [DataType(DataType.Date, ErrorMessage = "Data de validade inválida.")]
    public DateOnly? ValidadeCartaConducao { get; set; }

    [MaxLength(50, ErrorMessage = "ID da transportadora não pode exceder 50 caracteres.")]
    public string? TransportadoraId { get; set; }

    public bool Ativo { get; set; } = true;
}