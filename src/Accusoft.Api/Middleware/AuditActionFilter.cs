using System.Text;
using System.Text.Json;
using Accusoft.Api.Extensions;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Accusoft.Api.Middleware;

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditService _audit;
    private readonly ILogger<AuditActionFilter> _logger;

    // Paths to skip audit logging (sensitive operations logged separately)
    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/logout",
        "/api/auth/register",
    };

    public AuditActionFilter(IAuditService audit, ILogger<AuditActionFilter> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!IsStateChanging(request.Method))
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;

        if (ShouldSkipPath(path))
        {
            await next();
            return;
        }

        // Read body before execution
        object? payload = null;
        try
        {
            payload = await ReadRequestBodySafeAsync(request);
        }
        catch
        {
            // Non-critical — continue without payload
        }

        var executed = await next();

        // Only audit successful state changes
        if (executed.HttpContext.Response.StatusCode >= 400)
            return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        int userId;
        try { userId = context.HttpContext.User.GetUserId(); }
        catch { return; }

        var actionName = GetFriendlyActionName(path, request.Method);

        var details = new Dictionary<string, object?>
        {
            ["path"]   = path,
            ["method"] = request.Method,
        };

        if (!string.IsNullOrWhiteSpace(request.QueryString.Value))
            details["query"] = request.QueryString.Value;

        if (payload is not null)
            details["payload"] = payload;

        try
        {
            await _audit.LogAsync(userId, actionName, details, GetClientIp(context.HttpContext));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for {Path}", path);
        }
    }

    private static bool IsJsonRequest(HttpRequest request) =>
        request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<object?> ReadRequestBodySafeAsync(HttpRequest request)
    {
        if (!IsJsonRequest(request))
            return null;

        var contentLength = request.ContentLength.GetValueOrDefault();
        if (contentLength <= 0 || contentLength > 16_384)
            return null;

        try
        {
            request.EnableBuffering();
            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var content = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Sanitize sensitive fields from payload before logging
            using var doc = JsonDocument.Parse(content);
            var sanitized = SanitizePayload(doc.RootElement);
            return sanitized;
        }
        catch
        {
            try { request.Body.Position = 0; } catch { }
            return null;
        }
    }

    /// <summary>
    /// Removes sensitive fields (passwords, tokens, hashes) from audit payload.
    /// </summary>
    private static object? SanitizePayload(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "senha", "senhaHash", "token", "hash",
            "currentPassword", "newPassword", "secret", "key"
        };

        var result = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            if (sensitiveKeys.Contains(prop.Name))
                result[prop.Name] = "***";
            else
                result[prop.Name] = prop.Value.ToString();
        }

        return result;
    }

    private static bool IsStateChanging(string method) =>
        method is "POST" or "PUT" or "PATCH" or "DELETE";

    private static bool ShouldSkipPath(string path)
    {
        var lower = path.ToLowerInvariant();

        foreach (var skip in SkipPaths)
            if (lower.StartsWith(skip, StringComparison.OrdinalIgnoreCase))
                return true;

        // Skip admin session termination (handled separately)
        if (lower.Contains("/api/admin/sessoes/") && lower.EndsWith("/terminar"))
            return true;

        return false;
    }

    private static string GetFriendlyActionName(string path, string method)
    {
        var p = path.ToLowerInvariant();

        if (p.Contains("/api/user/recepcao") && method == "POST")   return "Recepção criada";
        if (p.Contains("/api/user/recepcao") && method == "PUT")    return "Recepção atualizada";
        if (p.Contains("/api/user/recepcao") && method == "DELETE") return "Recepção cancelada";
        if (p.EndsWith("/concluir") && method == "POST")            return "Recepção concluída";

        if (p.Contains("/api/user/atribuicoes") && method == "POST")   return "Atribuição criada";
        if (p.Contains("/api/user/atribuicoes") && method == "PUT")    return "Atribuição atualizada";
        if (p.Contains("/api/user/atribuicoes") && method == "DELETE") return "Atribuição cancelada";

        if (p.Contains("/api/user/gestao-viagens") && method == "POST")   return "Viagem criada";
        if (p.Contains("/api/user/gestao-viagens") && method == "PUT")    return "Viagem atualizada";
        if (p.EndsWith("/iniciar") && method == "POST")                   return "Viagem iniciada";
        if (p.EndsWith("/concluir") && method == "POST")                  return "Viagem concluída";

        if (p.Contains("/api/user/incidentes") && method == "POST")                      return "Incidente criado";
        if (p.Contains("/api/user/incidentes") && method == "PUT")                       return "Incidente atualizado";
        if (p.Contains("/api/user/incidentes") && p.EndsWith("/resolver") && method == "POST") return "Incidente resolvido";

        if (p.Contains("/api/user/produtos") && method == "POST")   return "Produto criado";
        if (p.Contains("/api/user/produtos") && method == "PUT")    return "Produto atualizado";
        if (p.Contains("/api/user/produtos") && method == "DELETE") return "Produto eliminado";

        if (p.Contains("/api/user/fornecedores") && method == "POST")   return "Fornecedor criado";
        if (p.Contains("/api/user/fornecedores") && method == "PUT")    return "Fornecedor atualizado";
        if (p.Contains("/api/user/fornecedores") && method == "DELETE") return "Fornecedor eliminado";

        if (p.Contains("/api/user/transportadoras") && method == "POST")   return "Transportadora criada";
        if (p.Contains("/api/user/transportadoras") && method == "PUT")    return "Transportadora atualizada";
        if (p.Contains("/api/user/transportadoras") && method == "DELETE") return "Transportadora eliminada";

        if (p.Contains("/api/motoristas") && method == "POST")   return "Motorista criado";
        if (p.Contains("/api/motoristas") && method == "PUT")    return "Motorista atualizado";
        if (p.Contains("/api/motoristas") && method == "DELETE") return "Motorista eliminado";

        if (p.Contains("/api/user/veiculos") && method == "POST")   return "Veículo criado";
        if (p.Contains("/api/user/veiculos") && method == "PUT")    return "Veículo atualizado";
        if (p.Contains("/api/user/veiculos") && method == "DELETE") return "Veículo eliminado";

        if (p.Contains("/api/user/faturas") && method == "POST")   return "Fatura criada";
        if (p.Contains("/api/user/faturas") && method == "PUT")    return "Fatura atualizada";
        if (p.Contains("/api/user/faturas") && method == "DELETE") return "Fatura eliminada";

        if (p.Contains("/api/admin/users/toggle") && method == "POST") return "Utilizador alterado";
        if (p.Contains("/api/admin/users/") && method == "DELETE")     return "Utilizador eliminado";

        return $"{method} {path}";
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',').First().Trim();

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp;

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
