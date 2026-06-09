using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

public class FornecedorResponseDto
{
    public int    Id               { get; set; }
    public string Codigo           { get; set; } = string.Empty;
    public string Nome             { get; set; } = string.Empty;
    public string? Nif             { get; set; }
    public string? Telefone        { get; set; }
    public string? Email           { get; set; }
    public string? Morada          { get; set; }
    public string? Localidade      { get; set; }
    public string? CodigoPostal    { get; set; }
    public string? Pais            { get; set; }
    public string? ContactoNome    { get; set; }
    public string? ContactoTelefone{ get; set; }
    public string? Observacoes     { get; set; }
    public bool   Ativo            { get; set; }
    public DateTimeOffset CriadoEm    { get; set; }
    public DateTimeOffset AtualizadoEm{ get; set; }
}

public class FornecedorCreateDto
{
    [Required(ErrorMessage = "Nome do fornecedor é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$",
        ErrorMessage = "NIF inválido (5-20 caracteres alfanuméricos).")]
    public string? Nif { get; set; }

    [MaxLength(30)]
    public string? Telefone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Endereço de e-mail inválido.")]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Morada { get; set; }

    [MaxLength(100)]
    public string? Localidade { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(100)]
    public string? Pais { get; set; } = "Portugal";

    [MaxLength(150)]
    public string? ContactoNome { get; set; }

    [MaxLength(30)]
    public string? ContactoTelefone { get; set; }

    public string? Observacoes { get; set; }
}

public class FornecedorUpdateDto
{
    [MaxLength(50)]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "Nome do fornecedor é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$",
        ErrorMessage = "NIF inválido (5-20 caracteres alfanuméricos).")]
    public string? Nif { get; set; }

    [MaxLength(30)]
    public string? Telefone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Endereço de e-mail inválido.")]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Morada { get; set; }

    [MaxLength(100)]
    public string? Localidade { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(100)]
    public string? Pais { get; set; }

    [MaxLength(150)]
    public string? ContactoNome { get; set; }

    [MaxLength(30)]
    public string? ContactoTelefone { get; set; }

    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;
}
