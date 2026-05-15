using Accusoft.Api.Domain.Entities;
using Accusoft.Api.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace Accusoft.Api.Middleware;

/// <summary>
/// Middleware de auditoria automática do módulo ECM.
///
/// Captura por cada request ao módulo documental:
///   • IP de origem (com suporte a proxies — X-Forwarded-For)
///   • User-Agent
///   • CorrelationId (gerado ou propagado do header X-Correlation-Id)
///   • TenantId (do JWT)
///   • UserId e Email (do JWT)
///   • Roles do utilizador (snapshot no momento do acesso)
///   • Método HTTP e endpoint
///   • HTTP Status Code da resposta
///   • Tempo de resposta em milissegundos
///   • Tamanho da resposta em bytes
///
/// O CorrelationId é injetado em:
///   1. HttpContext.Items["CorrelationId"] — para uso interno nos serviços
///   2. Response header X-Correlation-Id — para rastreabilidade cliente-servidor
///   3. Serilog LogContext — para correlação em todos os logs do request
///
/// Só executa para rotas que começam com /api/v1/documentos.
/// </summary>
public sealed class AuditoriaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditoriaMiddleware> _logger;

    // Prefixos de rota monitorizados pelo módulo ECM
    private const string RotaEcm = "/api/v1/documentos";

    public AuditoriaMiddleware(RequestDelegate next, ILogger<AuditoriaMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        // Bypass para rotas fora do módulo ECM
        if (!context.Request.Path.StartsWithSegments(RotaEcm, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // ── 1. Gerar / Propagar CorrelationId ────────────────────────────────
        var correlationId = ExtrairOuGerarCorrelationId(context);
        context.Items["CorrelationId"] = correlationId;

        // Injetar no header de resposta (antes de chamar o próximo middleware)
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd("X-Correlation-Id", correlationId.ToString());
            return Task.CompletedTask;
        });

        // ── 2. Capturar dados do request ──────────────────────────────────────
        var ip = ObterIpOrigem(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var metodoHttp = context.Request.Method;
        var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
        var acao = DeterminarAcao(context.Request.Method, context.Request.Path);

        var (tenantId, utilizadorId, utilizadorEmail, rolesJson) = ExtrairIdentidade(context);

        // ── 3. Enriquecer Serilog com contexto distribuído ────────────────────
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TenantId"] = tenantId,
            ["UserId"] = utilizadorId ?? "anonymous",
            ["IP"] = ip,
            ["Endpoint"] = endpoint
        });

        _logger.LogDebug(
            "ECM Request iniciado. {Metodo} {Endpoint} User={User} CorrelationId={CorrId}",
            metodoHttp, endpoint, utilizadorId ?? "anonymous", correlationId);

        // ── 4. Medir tempo de resposta ────────────────────────────────────────
        var stopwatch = Stopwatch.StartNew();
        string? mensagemErro = null;

        // Substituir o body stream para medir tamanho da resposta
        var bodyOriginal = context.Response.Body;
        using var bodyMedidor = new ContadorBytesStream(bodyOriginal);
        context.Response.Body = bodyMedidor;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            mensagemErro = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            context.Response.Body = bodyOriginal;

            var tempoMs = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;
            var tamanhoResposta = bodyMedidor.BytesEscritos;

            _logger.LogInformation(
                "ECM Request concluído. {Metodo} {Endpoint} Status={Status} " +
                "Tempo={TempoMs}ms Bytes={Bytes} CorrelationId={CorrId}",
                metodoHttp, endpoint, statusCode, tempoMs, tamanhoResposta, correlationId);

            // ── 5. Persistir registo de auditoria (fire-and-forget com scope próprio)
            _ = PersistirAuditoriaAsync(
                serviceProvider,
                correlationId,
                tenantId,
                ip,
                userAgent,
                metodoHttp,
                endpoint,
                acao,
                statusCode,
                tempoMs,
                utilizadorId,
                utilizadorEmail,
                rolesJson,
                mensagemErro,
                tamanhoResposta,
                DocumentoIdDaRota(context));
        }
    }

    // ─── Persistência Assíncrona (scope isolado) ──────────────────────────────

    /// <summary>
    /// Persiste o registo de auditoria num scope DI isolado.
    /// Fire-and-forget — não bloqueia a resposta ao cliente.
    /// Usa CancellationToken.None pois o HttpContext já terminou.
    /// </summary>
    private async Task PersistirAuditoriaAsync(
        IServiceProvider rootProvider,
        Guid correlationId,
        Guid tenantId,
        string ip,
        string userAgent,
        string metodoHttp,
        string endpoint,
        string acao,
        int statusCode,
        long tempoMs,
        string? utilizadorId,
        string? utilizadorEmail,
        string? rolesJson,
        string? mensagemErro,
        long tamanhoResposta,
        Guid? documentoId)
    {
        try
        {
            await using var scope = rootProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var auditoria = DocumentoAuditoria.Criar(
                correlationId: correlationId,
                tenantId: tenantId,
                ipOrigem: ip,
                userAgent: userAgent,
                metodoHttp: metodoHttp,
                endpoint: endpoint,
                acao: acao,
                httpStatusCode: statusCode,
                tempoRespostaMs: tempoMs,
                documentoId: documentoId,
                utilizadorId: utilizadorId,
                utilizadorEmail: utilizadorEmail,
                rolesJson: rolesJson,
                mensagemErro: mensagemErro,
                tamanhoRespostaBytes: tamanhoResposta);

            dbContext.DocumentosAuditoria.Add(auditoria);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Auditoria nunca deve derrubar o sistema — log e continuar
            _logger.LogError(ex,
                "Falha ao persistir auditoria. CorrelationId={CorrId} Endpoint={Endpoint}",
                correlationId, endpoint);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Guid ExtrairOuGerarCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var header)
            && Guid.TryParse(header.ToString(), out var existing))
            return existing;

        return Guid.NewGuid();
    }

    private static string ObterIpOrigem(HttpContext context)
    {
        // Suporte a load balancers e proxies reversos
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim(); // Primeiro IP = cliente original

        var realIp = context.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp;

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static (Guid tenantId, string? userId, string? email, string? rolesJson)
        ExtrairIdentidade(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return (Guid.Empty, null, null, null);

        var tenantStr = context.User.FindFirstValue("tenant_id")
                        ?? context.User.FindFirstValue("TenantId");
        var tenantId = Guid.TryParse(tenantStr, out var tid) ? tid : Guid.Empty;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub");
        var email = context.User.FindFirstValue(ClaimTypes.Email)
                    ?? context.User.FindFirstValue("email");

        var roles = context.User.FindAll(ClaimTypes.Role)
                           .Select(c => c.Value)
                           .ToList();
        var rolesJson = roles.Count > 0
            ? JsonSerializer.Serialize(roles)
            : null;

        return (tenantId, userId, email, rolesJson);
    }

    private static string DeterminarAcao(string metodo, PathString path)
    {
        var pathLower = path.Value?.ToLowerInvariant() ?? string.Empty;

        if (pathLower.EndsWith("/download")) return "Download";
        if (pathLower.EndsWith("/validar")) return "Validar";
        if (pathLower.EndsWith("/rejeitar")) return "Rejeitar";
        if (pathLower.EndsWith("/arquivar")) return "Arquivar";
        if (pathLower.EndsWith("/historico")) return "Historico";
        if (pathLower.EndsWith("/versoes")) return "NovaVersao";
        if (pathLower.EndsWith("/verificar-integridade")) return "VerificarIntegridade";

        return metodo switch
        {
            "GET" => "Listar/Obter",
            "POST" => "Upload",
            "DELETE" => "Eliminar",
            "PUT" or "PATCH" => "Atualizar",
            _ => metodo
        };
    }

    private static Guid? DocumentoIdDaRota(HttpContext context)
    {
        // Tenta extrair o GUID da rota: /api/v1/documentos/{id}/...
        var segmentos = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segmentos is { Length: >= 4 }
            && Guid.TryParse(segmentos[3], out var id))
            return id;

        return null;
    }
}

// ─── Stream auxiliar — conta bytes escritos na resposta ──────────────────────

/// <summary>
/// Stream wrapper que conta os bytes escritos na resposta HTTP.
/// Permite registar o tamanho real da resposta na auditoria.
/// </summary>
internal sealed class ContadorBytesStream : Stream
{
    private readonly Stream _inner;
    public long BytesEscritos { get; private set; }

    public ContadorBytesStream(Stream inner) => _inner = inner;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        BytesEscritos += count;
        _inner.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
    {
        BytesEscritos += count;
        await _inner.WriteAsync(buffer.AsMemory(offset, count), ct);
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        BytesEscritos += buffer.Length;
        await _inner.WriteAsync(buffer, ct);
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        // NÃO dispor o stream interno — pertence ao ASP.NET Core
        base.Dispose(disposing);
    }
}
