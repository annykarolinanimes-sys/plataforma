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
    // Magic bytes for allowed image types
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] GifMagic87 = [0x47, 0x49, 0x46, 0x38, 0x37, 0x61];
    private static readonly byte[] GifMagic89 = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
    private static readonly byte[] WebpRiff = [0x52, 0x49, 0x46, 0x46];

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5 MB

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var uid = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
        return user is null ? NotFound() : Ok(MapUserDto(user));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        if (req is null)
            return BadRequest(new { message = "Dados inválidos." });

        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { message = "Nome é obrigatório." });

        var uid = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null) return NotFound();

        user.Nome = req.Nome.Trim()[..Math.Min(req.Nome.Trim().Length, 150)];
        user.Departamento = req.Departamento?.Trim()[..Math.Min((req.Departamento?.Trim().Length ?? 0), 100)];
        user.Cargo = req.Cargo?.Trim()[..Math.Min((req.Cargo?.Trim().Length ?? 0), 100)];
        user.Telefone = req.Telefone?.Trim()[..Math.Min((req.Telefone?.Trim().Length ?? 0), 20)];

        await db.SaveChangesAsync();
        return Ok(MapUserDto(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (req is null)
            return BadRequest(new { message = "Dados inválidos." });

        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest(new { message = "Password atual é obrigatória." });

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "Nova password é obrigatória." });

        if (req.NewPassword.Length < 8)
            return BadRequest(new { message = "Nova password deve ter pelo menos 8 caracteres." });

        if (req.NewPassword.Length > 128)
            return BadRequest(new { message = "Nova password não pode exceder 128 caracteres." });

        if (req.NewPassword == req.CurrentPassword)
            return BadRequest(new { message = "Nova password deve ser diferente da atual." });

        var uid = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.SenhaHash))
            return Unauthorized(new { message = "Password atual incorreta." });

        user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
        await db.SaveChangesAsync();

        return Ok(new { message = "Password alterada com sucesso." });
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar)
    {
        if (avatar is null || avatar.Length == 0)
            return BadRequest(new { message = "Ficheiro de avatar é obrigatório." });

        if (avatar.Length > MaxAvatarSizeBytes)
            return BadRequest(new { message = "O ficheiro não pode exceder 5 MB." });

        // Validate content-type header
        if (!AllowedImageContentTypes.Contains(avatar.ContentType))
            return BadRequest(new { message = "Apenas imagens JPEG, PNG, GIF ou WebP são permitidas." });

        // Validate via magic bytes (prevents content-type spoofing)
        var magicValid = await ValidateImageMagicBytesAsync(avatar);
        if (!magicValid)
            return BadRequest(new { message = "Ficheiro inválido: o conteúdo não corresponde a uma imagem permitida." });

        // Validate file extension
        var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
            return BadRequest(new { message = "Extensão de ficheiro não permitida." });

        var uid = User.GetUserId();
        var user = await db.Users.FindAsync(uid);
        if (user is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try { fileStorage.Delete(user.AvatarUrl); }
            catch { /* non-critical: old file cleanup */ }
        }

        var (pathUrl, _, _) = await fileStorage.SaveAsync(avatar, uid);
        user.AvatarUrl = pathUrl;
        await db.SaveChangesAsync();

        return Ok(new { avatarUrl = pathUrl });
    }

    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas(
        [FromQuery] bool? lido,
        [FromQuery] string? tipo)
    {
        var uid = User.GetUserId();
        var query = db.Alertas.AsNoTracking().Where(a => a.UsuarioId == uid);

        if (lido.HasValue)
            query = query.Where(a => a.Lido == lido.Value);

        if (!string.IsNullOrWhiteSpace(tipo) &&
            Enum.TryParse<AlertaTipo>(tipo, ignoreCase: true, out var tipoEnum))
            query = query.Where(a => a.Tipo == tipoEnum);

        var alertas = await query.OrderByDescending(a => a.Data).ToListAsync();
        return Ok(alertas.Select(MapAlertaDto));
    }

    [HttpPatch("alertas/lidos")]
    public async Task<IActionResult> MarcarLidos([FromBody] MarcarLidoRequest req)
    {
        if (req is null || !req.Ids.Any())
            return BadRequest(new { message = "Lista de IDs inválida." });

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
        var uid = User.GetUserId();
        var query = db.Alertas.Where(a => a.UsuarioId == uid && !a.Lido);

        if (!string.IsNullOrWhiteSpace(tipo) &&
            Enum.TryParse<AlertaTipo>(tipo, ignoreCase: true, out var tipoEnum))
            query = query.Where(a => a.Tipo == tipoEnum);

        await query.ExecuteUpdateAsync(s => s.SetProperty(a => a.Lido, true));
        return NoContent();
    }

    [HttpGet("motoristas")]
    public async Task<IActionResult> GetMotoristas(
        [FromQuery] int? transportadoraId,
        [FromQuery] string? search,
        [FromQuery] bool? ativo)
    {
        var query = db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.User && u.Cargo == "Motorista");

        if (transportadoraId.HasValue)
            query = query.Where(u => u.TransportadoraId == transportadoraId.Value);

        if (ativo.HasValue)
            query = query.Where(u => u.Status == (ativo.Value ? UserStatus.Ativo : UserStatus.Inativo));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            var idFiltro = int.TryParse(search.Trim(), out var parsedId) ? parsedId : (int?)null;

            query = query.Where(u =>
                (idFiltro.HasValue && u.Id == idFiltro.Value) ||
                (u.Nome != null && u.Nome.ToLowerInvariant().Contains(s)) ||
                                (u.Telefone != null && u.Telefone.ToLowerInvariant().Contains(s)) ||
                (u.CartaConducao != null && u.CartaConducao.ToLowerInvariant().Contains(s)) ||
                (u.Email != null && u.Email.ToLowerInvariant().Contains(s)) ||
                (u.Departamento != null && u.Departamento.ToLowerInvariant().Contains(s)) ||
                (u.TransportadoraId.HasValue && u.TransportadoraId.Value.ToString().Contains(s)) ||
                (u.Transportadora != null && u.Transportadora.Nome.ToLowerInvariant().Contains(s)) ||
                (u.Cargo != null && u.Cargo.ToLowerInvariant().Contains(s)) ||
                (u.Role.ToString().ToLowerInvariant().Contains(s)) ||
                (u.Status.ToString().ToLowerInvariant().Contains(s)) ||
                (u.TransportadoraId.HasValue && u.TransportadoraId.Value.ToString().Contains(s)) ||
                (u.Transportadora != null && u.Transportadora.Nome.ToLowerInvariant().Contains(s)));
        }

        var motoristas = await query.OrderBy(u => u.Nome).ToListAsync();
        return Ok(motoristas.Select(MapMotoristaDto));
    }

    [HttpGet("motoristas/{id:int}")]
    public async Task<IActionResult> GetMotorista(int id)
    {
        var motorista = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        return Ok(MapMotoristaDto(motorista));
    }

    [HttpPost("motoristas")]
    public async Task<IActionResult> CreateMotorista([FromBody] CreateMotoristaRequest request)
    {
        if (request is null)
            return BadRequest(new { message = "Dados inválidos." });

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
            Nome = request.Nome.Trim(),
            Telefone = request.Telefone.Trim(),
            CartaConducao = request.CartaConducao.Trim(),
            Cargo = "Motorista",
            Role = UserRole.User,
            Status = UserStatus.Ativo,
            TransportadoraId = request.TransportadoraId,
            // Use a non-guessable placeholder email to satisfy unique constraint
            Email = $"motorista-{Guid.NewGuid():N}@internal.accusoft.local",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 11),
            DataCriacao = DateTimeOffset.UtcNow,
        };

        db.Users.Add(motorista);
        await db.SaveChangesAsync();

        return Ok(MapMotoristaDto(motorista));
    }

    [HttpPut("motoristas/{id:int}")]
    public async Task<IActionResult> UpdateMotorista(int id, [FromBody] UpdateMotoristaRequest request)
    {
        if (request is null)
            return BadRequest(new { message = "Dados inválidos." });

        var motorista = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(new { message = "Nome do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.Telefone))
            return BadRequest(new { message = "Telefone do motorista é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.CartaConducao))
            return BadRequest(new { message = "Carta de condução do motorista é obrigatória." });

        motorista.Nome = request.Nome.Trim();
        motorista.Telefone = request.Telefone.Trim();
        motorista.CartaConducao = request.CartaConducao.Trim();

        if (request.TransportadoraId.HasValue)
        {
            var transportadora = await db.TransportadorasCatalogo.FindAsync(request.TransportadoraId.Value);
            if (transportadora is null)
                return BadRequest(new { message = "Transportadora associada ao motorista não foi encontrada." });

            motorista.TransportadoraId = request.TransportadoraId.Value;
        }

        await db.SaveChangesAsync();
        return Ok(MapMotoristaDto(motorista));
    }

    [HttpDelete("motoristas/{id:int}")]
    public async Task<IActionResult> DeleteMotorista(int id)
    {
        var motorista = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Status = UserStatus.Inativo;
        await db.SaveChangesAsync();

        return Ok(new { message = "Motorista desativado com sucesso." });
    }

    [HttpPost("motoristas/{id:int}/ativar")]
    public async Task<IActionResult> ActivateMotorista(int id)
    {
        var motorista = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.User && u.Cargo == "Motorista");

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Status = UserStatus.Ativo;
        await db.SaveChangesAsync();

        return Ok(new { message = "Motorista ativado com sucesso." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates image files by checking their magic bytes (file signature),
    /// preventing content-type spoofing attacks.
    /// </summary>
    private static async Task<bool> ValidateImageMagicBytesAsync(IFormFile file)
    {
        try
        {
            var buffer = new byte[12];
            await using var stream = file.OpenReadStream();
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 12));

            if (bytesRead < 3) return false;

            // JPEG: FF D8 FF
            if (buffer[0] == JpegMagic[0] && buffer[1] == JpegMagic[1] && buffer[2] == JpegMagic[2])
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytesRead >= 8 && buffer.Take(8).SequenceEqual(PngMagic))
                return true;

            // GIF87a / GIF89a
            if (bytesRead >= 6 && (buffer.Take(6).SequenceEqual(GifMagic87) ||
                                    buffer.Take(6).SequenceEqual(GifMagic89)))
                return true;

            // WebP: RIFF????WEBP
            if (bytesRead >= 12 && buffer.Take(4).SequenceEqual(WebpRiff))
            {
                var webpMarker = buffer[8..12];
                if (webpMarker.SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 })) // "WEBP"
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
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
