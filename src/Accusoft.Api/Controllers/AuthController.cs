using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;


namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtService  _jwtService;
    private readonly IConfiguration _configuration;  

    public class LoginRequest
    {
        public string Email { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("senha")]
        public string Password { get; set; } = "";
    }

    public AuthController(AppDbContext context, IJwtService jwtService, IConfiguration configuration)
    {
        _context    = context;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower().Trim());

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.SenhaHash))
            return Unauthorized(new { message = "Email ou senha inválidos." });

        if (user.Status == UserStatus.Inativo)
            return Forbid();

        await _context.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoLogin, DateTimeOffset.UtcNow));

        var token = _jwtService.GenerateToken(user);

        return Ok(new
        {
            token  = token,
            user   = new
            {
                nome   = user.Nome,
                email  = user.Email,
                role   = user.Role.ToString().ToLowerInvariant(),   
                userId = user.Id,
            }
        });
    }



    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var emailNorm = req.Email.ToLower().Trim();

        if (await _context.Users.AnyAsync(u => u.Email == emailNorm))
            return Conflict(new { message = "Email já está em uso." });

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Senha, workFactor: 11);

        var user = new User
        {
            Nome         = req.Nome.Trim(),
            Email        = emailNorm,
            SenhaHash    = hash,
            Role         = UserRole.User,
            Status       = UserStatus.Ativo,
            Departamento = req.Departamento,
            Cargo        = req.Cargo,
            Telefone     = req.Telefone,
            DataCriacao  = DateTimeOffset.UtcNow,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);

        return Created($"/api/users/{user.Id}", new
        {
            token  = token,
            nome   = user.Nome,
            email  = user.Email,
            role   = user.Role.ToString().ToLowerInvariant(),
            userId = user.Id,
        });
    }

}
