using System.IdentityModel.Tokens.Jwt;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Accusoft.Api.Middleware;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessaoService sessaoService)
    {
        // Skip middleware for certain endpoints
        var path = context.Request.Path.Value?.ToLower();
        if (path?.StartsWith("/api/auth/login") == true ||
            path?.StartsWith("/api/auth/register") == true ||
            path?.StartsWith("/swagger") == true ||
            path?.StartsWith("/api/user/alertas") == true ||
            path?.StartsWith("/chatHub") == true)
        {
            await _next(context);
            return;
        }

        // Extract sessionId from JWT token
        var authorization = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
        {
            await _next(context);
            return;
        }

        var token = authorization.Substring("Bearer ".Length);
        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtToken = jwtHandler.ReadJwtToken(token);

            var sessionId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sessionId")?.Value;
            if (!string.IsNullOrEmpty(sessionId))
            {
                // Validate session
                var isValid = await sessaoService.ValidarSessaoAsync(sessionId);
                if (isValid)
                {
                    // Update session activity
                    await sessaoService.AtualizarAtividadeAsync(sessionId);
                }
                else
                {
                    _logger.LogWarning("Invalid session {SessionId} for request {Path}", sessionId, path);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Session expired or invalid");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session for request {Path}", path);
            // Continue processing even if session validation fails
        }

        await _next(context);
    }
}