using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.Controllers;

public record MotoristaResponseDto(
    int Id,
    string Nome,
    string Telefone,
    string CartaConducao,
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

    [MaxLength(50, ErrorMessage = "ID da transportadora não pode exceder 50 caracteres.")]
    public string? TransportadoraId { get; set; }

    public bool Ativo { get; set; } = true;
}