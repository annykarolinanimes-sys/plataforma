using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Helpers;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController(AppDbContext db, IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var uid  = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
        return user is null ? NotFound() : Ok(MapUserDto(user));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var uid  = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null) return NotFound();

        user.Nome        = req.Nome.Trim();
        user.Departamento = req.Departamento;
        user.Cargo       = req.Cargo;
        user.Telefone    = req.Telefone;

        await db.SaveChangesAsync();
        return Ok(MapUserDto(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest(new { message = "Current password is required." });

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "New password is required." });

        if (req.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters." });

        var uid  = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null)
            return NotFound(new { message = "User not found." });

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.SenhaHash))
            return Unauthorized(new { message = "Current password is incorrect." });

        user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
        await db.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
            return BadRequest(new { message = "Ficheiro de avatar é obrigatório." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(avatar.ContentType))
            return BadRequest(new { message = "Apenas ficheiros de imagem (JPEG, PNG, GIF, WebP) são permitidos." });

        if (avatar.Length > 5 * 1024 * 1024) // 5 MB
            return BadRequest(new { message = "O ficheiro não pode exceder 5 MB." });

        var uid  = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarUrl))
            fileStorage.Delete(user.AvatarUrl);

        // Save new avatar
        var (pathUrl, _, _) = await fileStorage.SaveAsync(avatar, uid);
        user.AvatarUrl = pathUrl;
        await db.SaveChangesAsync();

        return Ok(new { avatarUrl = pathUrl });
    }

    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas(
        [FromQuery] bool?   lido,
        [FromQuery] string? tipo)
    {
        var uid   = User.GetUserId();
        var query = db.Alertas.AsNoTracking().Where(a => a.UsuarioId == uid);

        if (lido.HasValue)
            query = query.Where(a => a.Lido == lido.Value);

        if (!string.IsNullOrWhiteSpace(tipo) &&
            Enum.TryParse<AlertaTipo>(tipo, ignoreCase: true, out var tipoEnum))
        {
            query = query.Where(a => a.Tipo == tipoEnum);
        }

        var alertas = await query.OrderByDescending(a => a.Data).ToListAsync();
        return Ok(alertas.Select(MapAlertaDto));
    }

    [HttpPatch("alertas/lidos")]
    public async Task<IActionResult> MarcarLidos([FromBody] MarcarLidoRequest req)
    {
        var uid = User.GetUserId();
        var ids = req.Ids.ToList();
        await db.Alertas
            .Where(a => ids.Contains(a.Id) && a.UsuarioId == uid)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Lido, true));
        return NoContent();
    }

    [HttpPatch("alertas/todos-lidos")]
    public async Task<IActionResult> MarcarTodosLidos([FromQuery] string? tipo)
    {
        var uid   = User.GetUserId();
        var query = db.Alertas.Where(a => a.UsuarioId == uid && !a.Lido);

        if (!string.IsNullOrWhiteSpace(tipo) &&
            Enum.TryParse<AlertaTipo>(tipo, ignoreCase: true, out var tipoEnum))
        {
            query = query.Where(a => a.Tipo == tipoEnum);
        }

        await query.ExecuteUpdateAsync(s => s.SetProperty(a => a.Lido, true));
        return NoContent();
    }

    [HttpGet("motoristas")]
    public async Task<IActionResult> GetMotoristas(
        [FromQuery] int? transportadoraId,
        [FromQuery] string? search)
    {
        var query = db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.User && u.Cargo == "Motorista");

        if (transportadoraId.HasValue)
            query = query.Where(u => u.TransportadoraId == transportadoraId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Nome.ToLower().Contains(search.ToLower()));

        var motoristas = await query.OrderBy(u => u.Nome).ToListAsync();
        return Ok(motoristas.Select(MapMotoristaDto));
    }

    [HttpGet("motoristas/{id:int}")]
    public async Task<IActionResult> GetMotorista(int id)
    {
        var motorista = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");
        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        return Ok(MapMotoristaDto(motorista));
    }

    [HttpPost("motoristas")]
    public async Task<IActionResult> CreateMotorista([FromBody] CreateMotoristaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(new { message = "Nome do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.Telefone))
            return BadRequest(new { message = "Telefone do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.CartaConducao))
            return BadRequest(new { message = "Carta de condução do motorista é obrigatória." });

        var transportadora = await db.TransportadorasCatalogo.FindAsync(request.TransportadoraId);
        if (transportadora is null)
            return BadRequest(new { message = "Transportadora associada ao motorista não foi encontrada." });

        var motorista = new User
        {
            Nome             = request.Nome.Trim(),
            Telefone         = request.Telefone.Trim(),
            CartaConducao     = request.CartaConducao.Trim(),
            Cargo            = "Motorista",
            Role             = UserRole.User,
            Status           = UserStatus.Ativo,
            TransportadoraId = request.TransportadoraId,
            Email            = $"motorista-{Guid.NewGuid():N}@local",
            SenhaHash        = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 11),
            DataCriacao      = DateTimeOffset.UtcNow,
        };

        db.Users.Add(motorista);
        await db.SaveChangesAsync();

        return Ok(MapMotoristaDto(motorista));
    }

    [HttpPut("motoristas/{id:int}")]
    public async Task<IActionResult> UpdateMotorista(int id, [FromBody] UpdateMotoristaRequest request)
    {
        var motorista = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");
        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(new { message = "Nome do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.Telefone))
            return BadRequest(new { message = "Telefone do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.CartaConducao))
            return BadRequest(new { message = "Carta de condução do motorista é obrigatória." });

        motorista.Nome          = request.Nome.Trim();
        motorista.Telefone      = request.Telefone.Trim();
        motorista.CartaConducao = request.CartaConducao.Trim();

        await db.SaveChangesAsync();
        return Ok(MapMotoristaDto(motorista));
    }

    private static MotoristaDto MapMotoristaDto(User u) => new(
        u.Id,
        u.Nome,
        u.Telefone ?? string.Empty,
        u.CartaConducao ?? string.Empty,
        u.TransportadoraId,
        u.Cargo ?? string.Empty,
        u.Role.ToApiString(),
        u.Status.ToApiString());

    private static UserDto MapUserDto(User u) => new(
        u.Id, u.Nome, u.Email,
        u.Role.ToApiString(),
        u.Status.ToApiString(),
        u.Departamento, u.Cargo, u.Telefone, u.AvatarUrl,
        u.DataCriacao, u.UltimoLogin);

    internal static EnvioDto MapEnvioDto(Envio e) => new(
        e.Id, e.IdString, e.NomeEquipamento, e.DataPrevista,
        e.Estado.ToApiString(),          
        e.UsuarioId, e.Usuario?.Nome ?? string.Empty,
        e.DataCriacao, e.DataAtualizacao,
        e.Documentos?.Select(d => new DocumentoDto(
            d.Id, d.Nome, d.PathUrl,
            d.Tipo.ToApiString(),            
            d.TamanhoBytes,
            TamanhoHelper.Legivel(d.TamanhoBytes),
            d.UsuarioId, d.EnvioId,
            d.DataUpload, d.DataAbertura)) ?? Enumerable.Empty<DocumentoDto>());

    private static AlertaDto MapAlertaDto(Alerta a) => new(
        a.Id,
        a.Tipo.ToApiString(),           
        a.Mensagem, a.Detalhe, a.Lido, a.Data,
        null, null);
}