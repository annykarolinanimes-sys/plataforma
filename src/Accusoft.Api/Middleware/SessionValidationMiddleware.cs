using System.IdentityModel.Tokens.Jwt;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Accusoft.Api.Middleware;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    // Paths that bypass session validation entirely (public endpoints)
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/logout",
        "/swagger",
        "/chatHub",
        "/health",
    };

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessaoService sessaoService, IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Allow public paths without session check
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var authorization = context.Request.Headers["Authorization"].FirstOrDefault();

        // No token present — let the [Authorize] attribute handle it
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = authorization["Bearer ".Length..].Trim();

        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();

            // Basic format check before parsing
            if (!jwtHandler.CanReadToken(token))
            {
                await _next(context);
                return;
            }

            var jwtToken = jwtHandler.ReadJwtToken(token);
            var sessionId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sessionId")?.Value;

            if (!string.IsNullOrEmpty(sessionId))
            {
                var isValid = await sessaoService.ValidarSessaoAsync(sessionId);

                if (!isValid)
                {
                    _logger.LogWarning("Session {SessionId} is expired or invalid. Path={Path}",
                        sessionId, path);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"message\":\"Sessão expirada ou inválida. Por favor, faça login novamente.\"}");
                    return;
                }

                context.Items["SessionIdToRefresh"] = sessionId;
            }
        }
        catch (Exception ex)
        {
            // Malformed token — log and let auth middleware reject it
            _logger.LogWarning(ex, "Error reading JWT token for request {Path}", path);
        }

        await _next(context);

        if (context.Items.TryGetValue("SessionIdToRefresh", out var refreshTokenIdObj) &&
            refreshTokenIdObj is string refreshSessionId)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var scopedSessaoService = scope.ServiceProvider.GetRequiredService<ISessaoService>();
                await scopedSessaoService.AtualizarAtividadeAsync(refreshSessionId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to refresh session activity for {SessionId}", refreshSessionId);
            }
        }
    }

    private static bool IsPublicPath(string path)
    {
        foreach (var publicPath in PublicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
