using Accusoft.Api.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Accusoft.Api.DTOs;

// ═════════════════════════════════════════════════════════════════════════════
// RESULT PATTERN — Evita exceções no fluxo de controlo normal
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Resultado tipado de operações de aplicação.
/// Sucesso transporta T; falha transporta código e mensagem de erro.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Erro { get; }
    public ErroTipo TipoErro { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string erro, ErroTipo tipo) { IsSuccess = false; Erro = erro; TipoErro = tipo; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Falha(string erro, ErroTipo tipo = ErroTipo.Validacao) => new(erro, tipo);
    public static Result<T> NaoEncontrado(string entidade, Guid id) =>
        new($"{entidade} com ID '{id}' não foi encontrado.", ErroTipo.NaoEncontrado);
    public static Result<T> AcessoNegado(string razao) =>
        new(razao, ErroTipo.AcessoNegado);
    public static Result<T> Conflito(string razao) =>
        new(razao, ErroTipo.Conflito);
}

public enum ErroTipo
{
    Validacao,
    NaoEncontrado,
    AcessoNegado,
    Conflito,
    Interno
}

// ═════════════════════════════════════════════════════════════════════════════
// UPLOAD
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Command de upload de um novo documento.</summary>
public sealed record UploadDocumentoCommand(
    IFormFile Ficheiro,
    CategoriaDocumento Categoria,
    ContextoDocumento Contexto,
    Guid? EntidadeAssociadaId,
    string? Descricao
);

public sealed record UploadDocumentoStreamCommand(
    Stream FicheiroStream,
    string NomeOriginal,
    string MimeType,
    long TamanhoBytes,
    CategoriaDocumento Categoria,
    ContextoDocumento Contexto,
    Guid? EntidadeAssociadaId,
    string? Descricao
);

/// <summary>Command de nova versão de um documento existente.</summary>
public sealed record NovaVersaoDocumentoCommand(
    Guid DocumentoId,
    IFormFile Ficheiro,
    string? Descricao
);

// ═════════════════════════════════════════════════════════════════════════════
// WORKFLOW
// ═════════════════════════════════════════════════════════════════════════════

public sealed record ValidarDocumentoCommand(Guid DocumentoId);

public sealed record RejeitarDocumentoCommand(
    Guid DocumentoId,
    string Comentario   // Obrigatório para compliance
);

public sealed record ArquivarDocumentoCommand(
    Guid DocumentoId,
    string Motivo
);

public sealed record EliminarDocumentoCommand(
    Guid DocumentoId,
    string Razao       // Obrigatório para compliance
);

// ═════════════════════════════════════════════════════════════════════════════
// RESPONSE DTOs
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>DTO de resposta completo de um Documento.</summary>
public sealed record DocumentoResponse(
    Guid Id,
    Guid TenantId,
    Guid CorrelationId,
    string NomeOriginal,
    long TamanhoBytes,
    string TamanhoBytesFormatado,
    string MimeTypeValidado,
    string Extensao,
    string HashSHA256,
    DateTimeOffset HashCalculadoEm,
    CategoriaDocumento Categoria,
    ContextoDocumento Contexto,
    Guid? EntidadeAssociadaId,
    string? Descricao,
    EstadoDocumento Estado,
    string? RazaoEstado,
    bool ScanAntivirusRealizado,
    ResultadoScan? ResultadoScanAntivirus,
    bool Encriptado,
    bool RetencaoLegalAtiva,
    DateTimeOffset? DataExpiracaoRetencao,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    bool Validado,
    string? ValidadoPor,
    DateTimeOffset? ValidadoEm,
    string? ComentarioRejeicao,
    string? RejeitadoPor,
    DateTimeOffset? RejeitadoEm,
    int Versao,
    bool IsLatest,
    Guid? DocumentoOrigemId,
    Guid? VersaoAnteriorId,
    bool IntegridadeVerificada,
    DateTimeOffset? UltimaVerificacaoIntegridade,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt
);

/// <summary>DTO resumido para listagens (performance — campos essenciais).</summary>
public sealed record DocumentoResumoResponse(
    Guid Id,
    string NomeOriginal,
    string MimeTypeValidado,
    string TamanhoBytesFormatado,
    EstadoDocumento Estado,
    CategoriaDocumento Categoria,
    ContextoDocumento Contexto,
    int Versao,
    bool IsLatest,
    bool Validado,
    bool IntegridadeVerificada,
    DateTimeOffset CreatedAt,
    string CreatedBy
);

/// <summary>DTO de entrada de histórico.</summary>
public sealed record DocumentoHistoricoResponse(
    Guid Id,
    TipoOperacaoHistorico TipoOperacao,
    string Descricao,
    string ExecutadoPor,
    string? IpOrigem,
    string? EstadoAnterior,
    string? EstadoPosterior,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm
);

/// <summary>Resultado de operação de upload com metadados de segurança.</summary>
public sealed record UploadDocumentoResponse(
    Guid DocumentoId,
    string NomeOriginal,
    string HashSHA256,
    string MimeTypeDetectado,
    long TamanhoBytes,
    int Versao,
    EstadoDocumento Estado,
    Guid CorrelationId,
    string Mensagem
);

/// <summary>Resultado de verificação de integridade.</summary>
public sealed record VerificacaoIntegridadeResponse(
    Guid DocumentoId,
    bool Valido,
    string HashEsperado,
    string HashAtual,
    DateTimeOffset VerificadoEm,
    string? Mensagem
);

/// <summary>DTO para listagem de documentos (Domain Entity).</summary>
public sealed record DocumentoListDto(
    Guid Id,
    string NomeOriginal,
    string CaminhoRelativo,
    string Categoria,
    long TamanhoBytes,
    string TamanhoFormatado,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    EstadoDocumento Estado
);

// ═════════════════════════════════════════════════════════════════════════════
// PAGINAÇÃO / FILTROS
// ═════════════════════════════════════════════════════════════════════════════

public sealed record ListarDocumentosQuery(
    int Pagina = 1,
    int TamanhoPagina = 25,
    EstadoDocumento? Estado = null,
    CategoriaDocumento? Categoria = null,
    ContextoDocumento? Contexto = null,
    Guid? EntidadeAssociadaId = null,
    bool? ApenasLatest = true,
    string? PesquisaNome = null,
    DateTimeOffset? CriadoApos = null,
    DateTimeOffset? CriadoAntes = null
);

