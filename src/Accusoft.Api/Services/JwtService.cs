using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Accusoft.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Accusoft.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user, string? sessionId = null);
}

public class JwtService(IConfiguration config, ILogger<JwtService> logger) : IJwtService
{
    public string GenerateToken(User user, string? sessionId = null)
    {
        if (user is null)
            throw new ArgumentNullException(nameof(user));

        var rawKey = config["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key não configurada.");

        if (rawKey.Length < 32)
            throw new InvalidOperationException("JWT Key deve ter pelo menos 32 caracteres.");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        if (!double.TryParse(config["Jwt:ExpiresHours"], out var expiresHours) || expiresHours <= 0)
            expiresHours = 8;

        var expires = DateTime.UtcNow.AddHours(expiresHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name,  user.Nome),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()),
            new("userId", user.Id.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(sessionId))
            claims.Add(new Claim("sessionId", sessionId));

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expires,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        logger.LogDebug("JWT generated for user {UserId}, expires {Expires}", user.Id, expires);

        return tokenString;
    }
}
