using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]   
public class AdminController(AppDbContext db, IAuditService audit) : ControllerBase
{
    
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
        {
            query = query.Where(u => u.Role == roleEnum);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<UserStatus>(status, ignoreCase: true, out var statusEnum))
        {
            query = query.Where(u => u.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.Nome.ToLower().Contains(search.ToLower()) ||
                u.Email.ToLower().Contains(search.ToLower()));

        var users = await query.OrderBy(u => u.Nome).ToListAsync();
        return Ok(users.Select(MapUserDto));
    }

    [HttpPost("users/toggle")]
    public async Task<IActionResult> ToggleUser([FromBody] ToggleUserRequest req)
    {
        var adminId = User.GetUserId();
        var target  = await db.Users.FindAsync(req.UserId);

        if (target is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        if (target.Id == adminId)
            return BadRequest(new { message = "Não pode desativar a sua própria conta." });

        var estadoAnterior = target.Status;

        target.Status = target.Status == UserStatus.Ativo
            ? UserStatus.Inativo
            : UserStatus.Ativo;

        await db.SaveChangesAsync();

        await audit.LogAsync(adminId, "USER_TOGGLE", new
        {
            targetUserId = target.Id,
            targetEmail  = target.Email,
            de           = estadoAnterior.ToApiString(),
            para         = target.Status.ToApiString(),
        }, GetClientIp());

        return Ok(new
        {
            userId     = target.Id,
            novoStatus = target.Status.ToApiString(),
        });
    }


    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalAtivos     = await db.Users.CountAsync(u => u.Status == UserStatus.Ativo);
        var totalInativos   = await db.Users.CountAsync(u => u.Status == UserStatus.Inativo);
        var totalAlertas    = await db.Alertas.CountAsync();
        var alertasNaoLidos = await db.Alertas.CountAsync(a => !a.Lido);

        return Ok(new AdminStatsDto(
            totalAtivos, totalInativos,
            totalAlertas, alertasNaoLidos));
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int     page    = 1,
        [FromQuery] int     perPage = 50,
        [FromQuery] string? acao    = null)
    {
        var query = db.AuditLogs
            .AsNoTracking()
            .Include(al => al.Admin);

        var filtered = string.IsNullOrWhiteSpace(acao)
            ? query
            : query.Where(al => al.Acao == acao.ToUpper());

        var total = await filtered.CountAsync();

        var logs = await filtered
            .OrderByDescending(al => al.Timestamp)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(al => new AuditLogDto(
                al.Id, al.AdminId, al.Admin.Nome,
                al.Acao, al.Detalhe, al.IpAddress, al.Timestamp))
            .ToListAsync();

        return Ok(new { total, page, perPage, data = logs });
    }

       private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private static UserDto MapUserDto(User u) => new(
        u.Id, u.Nome, u.Email,
        u.Role.ToApiString(),
        u.Status.ToApiString(),
        u.Departamento, u.Cargo, u.Telefone, u.AvatarUrl,
        u.DataCriacao, u.UltimoLogin);
}
