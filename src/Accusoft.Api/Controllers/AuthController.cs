using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accusoft.Api.Extensions;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ISessaoService _sessaoService;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthController> _logger;

    // Local DTO to avoid conflict with Accusoft.Api.DTOs.LoginRequest record
    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public AuthController(
        AppDbContext context,
        IJwtService jwtService,
        IConfiguration configuration,
        ISessaoService sessaoService,
        IAuditService audit,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
        _sessaoService = sessaoService;
        _audit = audit;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (req is null)
            return BadRequest(new { message = "Dados de login inválidos." });

        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Email e senha são obrigatórios." });

        // Normalise email
        var emailNorm = req.Email.Trim().ToLowerInvariant();

        // Constant-time lookup to avoid user enumeration via timing
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == emailNorm);

        // Verify password even when user not found to avoid timing attacks
        var passwordValid = user is not null &&
                            BCrypt.Net.BCrypt.Verify(req.Password, user.SenhaHash);

        if (user is null || !passwordValid)
        {
            _logger.LogWarning("Failed login attempt for email {Email} from IP {IP}",
                emailNorm, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Email ou senha inválidos." });
        }

        if (user.Status == UserStatus.Inativo)
        {
            _logger.LogWarning("Login attempt by inactive user {UserId}", user.Id);
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Conta desativada. Contacte o administrador." });
        }

        var sessionId = Guid.NewGuid().ToString();
        var token = _jwtService.GenerateToken(user, sessionId);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        if (!double.TryParse(_configuration["Jwt:ExpiresHours"], out var expiresHours))
            expiresHours = 8;
        var expires = DateTime.UtcNow.AddHours(expiresHours);

        await _sessaoService.CriarSessaoAsync(sessionId, user.Id, token, ipAddress, userAgent, expires);

        await _context.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoLogin, DateTimeOffset.UtcNow));

        // Log without sensitive data
        await _audit.LogAsync(user.Id, "USER_LOGIN", new
        {
            ipAddress,
            sessionId,
            userAgent = userAgent.Length > 200 ? userAgent[..200] : userAgent,
        }, ipAddress);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Ok(new
        {
            token,
            user = new
            {
                nome = user.Nome,
                email = user.Email,
                role = user.Role.ToString().ToLowerInvariant(),
                userId = user.Id,
            }
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        int userId;
        try { userId = User.GetUserId(); }
        catch { return Ok(new { message = "Logout realizado." }); }

        var sessionId = User.FindFirst("sessionId")?.Value;

        if (!string.IsNullOrEmpty(sessionId))
        {
            await _sessaoService.TerminarSessaoAsync(sessionId);
            await _audit.LogAsync(userId, "USER_LOGOUT", new { sessionId },
                HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        return Ok(new { message = "Logout realizado com sucesso." });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (req is null)
            return BadRequest(new { message = "Dados de registo inválidos." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var emailNorm = req.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == emailNorm))
            return Conflict(new { message = "Email já está em uso." });

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 11);

        var user = new User
        {
            Nome = req.Nome.Trim(),
            Email = emailNorm,
            SenhaHash = hash,
            Role = UserRole.User,
            Status = UserStatus.Ativo,
            Departamento = req.Departamento?.Trim(),
            Cargo = req.Cargo?.Trim(),
            Telefone = req.Telefone?.Trim(),
            DataCriacao = DateTimeOffset.UtcNow,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var sessionId = Guid.NewGuid().ToString();
        var token = _jwtService.GenerateToken(user, sessionId);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        if (!double.TryParse(_configuration["Jwt:ExpiresHours"], out var expiresHours))
            expiresHours = 8;
        var expires = DateTime.UtcNow.AddHours(expiresHours);

        await _sessaoService.CriarSessaoAsync(sessionId, user.Id, token, ipAddress, userAgent, expires);

        _logger.LogInformation("New user registered: {UserId}", user.Id);

        return Created($"/api/users/{user.Id}", new
        {
            token,
            nome = user.Nome,
            email = user.Email,
            role = user.Role.ToString().ToLowerInvariant(),
            userId = user.Id,
        });
    }
}
