using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.DTOs;

// ─── DTO de leitura (response) ────────────────────────────────────────────────
public class DocumentoGeralResponseDto
{
    public int Id { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // Fatura, POD, Relatorio, Outro
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataDocumento { get; set; }
    public DateTime DataCriacao { get; set; }
    public string? CaminhoFicheiro { get; set; }
    public long TamanhoBytes { get; set; }
    public string? EntidadeRelacionada { get; set; } // Cliente, Fornecedor, etc.
    public int? EntidadeId { get; set; }
    public string? EntidadeNome { get; set; }
    public string? Tags { get; set; }
    public string? Categoria { get; set; }
    public bool Favorito { get; set; }
    public int Visualizacoes { get; set; }
    public int Downloads { get; set; }
    public DateTime? UltimoAcesso { get; set; }
    public string? Observacoes { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
}

// ─── DTO de criação ───────────────────────────────────────────────────────────
public class DocumentoGeralCreateDto
{
    [Required(ErrorMessage = "Tipo de documento é obrigatório.")]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nome do documento é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    public DateTime? DataDocumento { get; set; }

    public string? EntidadeRelacionada { get; set; }
    public int? EntidadeId { get; set; }
    public string? Tags { get; set; }
    public string? Categoria { get; set; }
    public string? Observacoes { get; set; }
}

// ─── DTO de actualização ──────────────────────────────────────────────────────
public class DocumentoGeralUpdateDto
{
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public string? Tags { get; set; }
    public string? Categoria { get; set; }
    public bool? Favorito { get; set; }
    public string? Observacoes { get; set; }
}

// ─── DTO para upload ─────────────────────────────────────────────────────────
public class DocumentoUploadDto
{
    public IFormFile Ficheiro { get; set; } = null!;
    public string Tipo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime? DataDocumento { get; set; }
    public string? EntidadeRelacionada { get; set; }
    public int? EntidadeId { get; set; }
    public string? Tags { get; set; }
    public string? Categoria { get; set; }
}

// ─── DTO para filtros ────────────────────────────────────────────────────────
public class FiltrarDocumentosDto
{
    public string? Tipo { get; set; }
    public string? Categoria { get; set; }
    public string? Search { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public int? EntidadeId { get; set; }
    public string? EntidadeRelacionada { get; set; }
    public bool? Favorito { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}