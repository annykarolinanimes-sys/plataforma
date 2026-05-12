using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Accusoft.Api.DTOs;   
using Accusoft.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Accusoft.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user, string? sessionId = null);
}

public class JwtService(IConfiguration config) : IJwtService
{
    public string GenerateToken(User user, string? sessionId = null)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(config["Jwt:ExpiresHours"] ?? "8"));

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.Nome),
            new Claim(ClaimTypes.Role,    user.Role.ToString().ToLowerInvariant()),
            new Claim("userId",                      user.Id.ToString()),
        };

        if (!string.IsNullOrEmpty(sessionId))
        {
            claims.Add(new Claim("sessionId", sessionId));
        }

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
