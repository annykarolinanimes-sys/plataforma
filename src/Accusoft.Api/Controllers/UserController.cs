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
public class UserController(AppDbContext db) : ControllerBase
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