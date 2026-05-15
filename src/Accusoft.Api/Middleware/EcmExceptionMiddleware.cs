using System.Net;
using System.Text.Json;

namespace Accusoft.Api.Middleware;

/// <summary>
/// Middleware de tratamento global de exceções do módulo ECM.
///
/// Objetivos:
///   • Nunca expor stack traces ou detalhes internos em produção
///   • Manter rastreabilidade via CorrelationId em todos os erros
///   • Mapear exceções de domínio para HTTP status codes adequados
///   • Logging estruturado de todas as exceções não tratadas
///   • Resposta JSON consistente (ProblemDetails — RFC 7807)
/// </summary>
public sealed class EcmExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EcmExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    // Opções de serialização consistentes com o resto da API
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public EcmExceptionMiddleware(
        RequestDelegate next,
        ILogger<EcmExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await TratarExcecaoAsync(context, ex);
        }
    }

    private async Task TratarExcecaoAsync(HttpContext context, Exception ex)
    {
        // Extrair CorrelationId injetado pelo AuditoriaMiddleware
        var correlationId = context.Items.TryGetValue("CorrelationId", out var corrObj)
                            && corrObj is Guid corrId
            ? corrId
            : Guid.NewGuid();

        // Mapear exceção para status code e mensagem
        var (statusCode, titulo, detalhe) = MapearExcecao(ex);

        // Logging estruturado com nível adequado por tipo de exceção
        var nivel = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;
        _logger.Log(nivel, ex,
            "ECM Exception. Status={Status} Tipo={Tipo} CorrelationId={CorrId} " +
            "Endpoint={Metodo} {Path}",
            statusCode,
            ex.GetType().Name,
            correlationId,
            context.Request.Method,
            context.Request.Path);

        // Construir ProblemDetails (RFC 7807)
        var problema = new EcmProblemDetails
        {
            Type = $"https://ecm.docs/erros/{statusCode}",
            Title = titulo,
            Status = statusCode,
            Detail = detalhe,
            Instance = context.Request.Path,
            CorrelationId = correlationId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Em desenvolvimento, incluir stack trace para facilitar debug
        if (_env.IsDevelopment())
            problema.StackTrace = ex.ToString();

        // Garantir que a resposta ainda não foi iniciada
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json; charset=utf-8";
            context.Response.Headers.TryAdd("X-Correlation-Id", correlationId.ToString());

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(problema, JsonOptions));
        }
        else
        {
            // Resposta já iniciada (ex: durante streaming de ficheiro)
            _logger.LogCritical(
                "Exceção após início de resposta. CorrelationId={CorrId} Tipo={Tipo}",
                correlationId, ex.GetType().Name);
        }
    }

    // ─── Mapeamento de Exceções ───────────────────────────────────────────────

    private static (int statusCode, string titulo, string detalhe) MapearExcecao(Exception ex)
        => ex switch
        {
            // ── Exceções de Domínio ECM ────────────────────────────────────────
            EcmDocumentoNaoEncontradoException e =>
                (404, "Documento Não Encontrado", e.Message),

            EcmAcessoNegadoException e =>
                (403, "Acesso Negado", e.Message),

            EcmEstadoInvalidoException e =>
                (409, "Transição de Estado Inválida", e.Message),

            EcmRetencaoLegalException e =>
                (423, "Bloqueado por Retenção Legal", e.Message),  // 423 Locked

            EcmMimeNaoPermitidoException e =>
                (415, "Tipo de Ficheiro Não Permitido", e.Message),  // 415 Unsupported Media Type

            EcmIntegridadeCompromissadaException e =>
                (409, "Integridade Comprometida", e.Message),

            EcmPersistenciaException e =>
                (500, "Erro de Persistência", SanitizarMensagem(e.Message)),

            // ── Exceções de Infraestrutura ─────────────────────────────────────
            UnauthorizedAccessException =>
                (401, "Não Autorizado", "Autenticação necessária."),

            InvalidOperationException e =>
                (409, "Operação Inválida", e.Message),

            ArgumentException e =>
                (400, "Argumento Inválido", e.Message),

            FileNotFoundException =>
                (404, "Ficheiro Não Encontrado",
                    "O ficheiro físico solicitado não foi encontrado no storage."),

            IOException e =>
                (500, "Erro de I/O", SanitizarMensagem(e.Message)),

            TaskCanceledException =>
                (499, "Pedido Cancelado", "O pedido foi cancelado pelo cliente."),

            OperationCanceledException =>
                (499, "Pedido Cancelado", "O pedido foi cancelado."),

            // ── Fallback ───────────────────────────────────────────────────────
            _ =>
                (500, "Erro Interno do Servidor",
                    "Ocorreu um erro inesperado. Consulte os logs com o CorrelationId fornecido.")
        };

    /// <summary>
    /// Remove informação técnica sensível de mensagens de erro em produção.
    /// Preserva a mensagem original em desenvolvimento.
    /// </summary>
    private static string SanitizarMensagem(string mensagem)
    {
        // Em produção, não expor detalhes de caminhos de ficheiro ou strings de ligação
        if (mensagem.Contains(":\\") || mensagem.Contains(":/") || mensagem.Contains("Server="))
            return "Erro interno. Consulte os logs com o CorrelationId.";

        return mensagem;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PROBLEM DETAILS ESTENDIDO
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>ProblemDetails (RFC 7807) estendido com campos ECM.</summary>
public sealed class EcmProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? StackTrace { get; set; } // Apenas em Development
}

// ═════════════════════════════════════════════════════════════════════════════
// EXCEÇÕES DE DOMÍNIO ECM
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Base para todas as exceções do domínio ECM.</summary>
public abstract class EcmException : Exception
{
    protected EcmException(string message) : base(message) { }
    protected EcmException(string message, Exception inner) : base(message, inner) { }
}

public sealed class EcmDocumentoNaoEncontradoException : EcmException
{
    public Guid DocumentoId { get; }
    public EcmDocumentoNaoEncontradoException(Guid id)
        : base($"Documento '{id}' não encontrado.")
        => DocumentoId = id;
}

public sealed class EcmAcessoNegadoException : EcmException
{
    public EcmAcessoNegadoException(string recurso, string razao)
        : base($"Acesso negado ao recurso '{recurso}': {razao}") { }
}

public sealed class EcmEstadoInvalidoException : EcmException
{
    public EcmEstadoInvalidoException(string estado, string operacao)
        : base($"Operação '{operacao}' não permitida no estado '{estado}'.") { }
}

public sealed class EcmRetencaoLegalException : EcmException
{
    public EcmRetencaoLegalException(Guid documentoId)
        : base($"Documento '{documentoId}' está sob retenção legal ativa. Operação bloqueada.") { }
}

public sealed class EcmMimeNaoPermitidoException : EcmException
{
    public EcmMimeNaoPermitidoException(string mimeDetectado, string motivo)
        : base($"Tipo de ficheiro '{mimeDetectado}' não permitido: {motivo}") { }
}

public sealed class EcmIntegridadeCompromissadaException : EcmException
{
    public Guid DocumentoId { get; }
    public EcmIntegridadeCompromissadaException(Guid id, string hashEsperado, string hashAtual)
        : base($"Integridade do documento '{id}' comprometida. Hash diverge.")
        => DocumentoId = id;
}

public sealed class EcmPersistenciaException : EcmException
{
    public EcmPersistenciaException(string message)
        : base(message) { }

    public EcmPersistenciaException(string message, Exception inner)
        : base(message, inner) { }
}
