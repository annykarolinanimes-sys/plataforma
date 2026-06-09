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
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ISessaoService _sessaoService;
    private readonly ILogger<AdminController> _logger;
    private readonly IHostEnvironment _env;

    public AdminController(
        AppDbContext db,
        IAuditService audit,
        ISessaoService sessaoService,
        ILogger<AdminController> logger,
        IHostEnvironment env)
    {
        _db = db;
        _audit = audit;
        _sessaoService = sessaoService;
        _logger = logger;
        _env = env;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
            query = query.Where(u => u.Role == roleEnum);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<UserStatus>(status, ignoreCase: true, out var statusEnum))
            query = query.Where(u => u.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(u =>
                u.Nome.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = users.Select(MapUserDto) });
    }

    [HttpPost("users/toggle")]
    public async Task<IActionResult> ToggleUser([FromBody] ToggleUserRequest req)
    {
        var adminId = User.GetUserId();
        var target = await _db.Users.FindAsync(req.UserId);

        if (target is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        if (target.Id == adminId)
            return BadRequest(new { message = "Não pode desativar a sua própria conta." });

        // Prevent disabling other admins
        if (target.Role == UserRole.Admin)
            return BadRequest(new { message = "Não é possível desativar outro administrador." });

        var estadoAnterior = target.Status;
        target.Status = target.Status == UserStatus.Ativo
            ? UserStatus.Inativo
            : UserStatus.Ativo;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(adminId, "USER_TOGGLE", new
        {
            targetUserId = target.Id,
            targetEmail = target.Email,
            de = estadoAnterior.ToApiString(),
            para = target.Status.ToApiString(),
        }, GetClientIp());

        _logger.LogInformation("Admin {AdminId} toggled user {TargetId} to {Status}",
            adminId, target.Id, target.Status);

        return Ok(new
        {
            userId = target.Id,
            novoStatus = target.Status.ToApiString(),
        });
    }

    [HttpDelete("users/{userId:int}")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var adminId = User.GetUserId();
        var target = await _db.Users.FindAsync(userId);

        if (target is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        if (target.Id == adminId)
            return BadRequest(new { message = "Não pode eliminar a sua própria conta." });

        if (target.Role == UserRole.Admin)
            return BadRequest(new { message = "Não é possível eliminar outro administrador." });

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(adminId, "USER_DELETED", new
        {
            targetUserId = target.Id,
            targetEmail = target.Email,
        }, GetClientIp());

        _logger.LogWarning("Admin {AdminId} deleted user {TargetId} ({Email})",
            adminId, target.Id, target.Email);

        return Ok(new { message = "Utilizador eliminado com sucesso." });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalAtivos = await _db.Users.CountAsync(u => u.Status == UserStatus.Ativo);
        var totalInativos = await _db.Users.CountAsync(u => u.Status == UserStatus.Inativo);
        var totalAlertas = await _db.Alertas.CountAsync();
        var alertasNaoLidos = await _db.Alertas.CountAsync(a => !a.Lido);
        var sessoesAtivas = await _db.Sessoes.CountAsync(s =>
            s.IsActive && s.DataExpiracao > DateTimeOffset.UtcNow);

        return Ok(new
        {
            totalAtivos,
            totalInativos,
            totalAlertas,
            alertasNaoLidos,
            sessoesAtivas,
            totalUsers = totalAtivos + totalInativos,
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 50,
        [FromQuery] string? acao = null,
        [FromQuery] string? search = null)
    {
        perPage = Math.Clamp(perPage, 1, 200);
        page = Math.Max(1, page);

        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(al => al.Admin);

        IQueryable<AuditLog> filtered = query;

        if (!string.IsNullOrWhiteSpace(acao))
            filtered = filtered.Where(al => al.Acao == acao.ToUpper());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            filtered = filtered.Where(al =>
                al.Acao.ToLower().Contains(s) ||
                al.Admin.Nome.ToLower().Contains(s) ||
                (al.IpAddress != null && al.IpAddress.Contains(s)));
        }

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

    [HttpGet("atividades/logins-recentes")]
    public async Task<IActionResult> GetLoginsRecentes([FromQuery] int limite = 10)
    {
        limite = Math.Clamp(limite, 1, 100);

        var logins = await _db.Users
            .AsNoTracking()
            .Where(u => u.UltimoLogin != null)
            .OrderByDescending(u => u.UltimoLogin)
            .Take(limite)
            .Select(u => new
            {
                id = u.Id,
                usuarioNome = u.Nome,
                usuarioEmail = u.Email,
                timestamp = u.UltimoLogin!.Value,
            })
            .ToListAsync();

        var userIds = logins.Select(l => l.id).ToList();
        var sessaoMap = await _db.Sessoes
            .AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                IpAddress = g.OrderByDescending(s => s.DataCriacao)
                             .Select(s => s.IpAddress)
                             .FirstOrDefault(),
            })
            .ToDictionaryAsync(x => x.UserId, x => x.IpAddress ?? "—");

        var result = logins.Select(l => new
        {
            l.id,
            l.usuarioNome,
            l.usuarioEmail,
            l.timestamp,
            ip = sessaoMap.TryGetValue(l.id, out var ip) ? ip : "—",
        });

        return Ok(result);
    }

    [HttpGet("atividades/acoes-frequentes")]
    public async Task<IActionResult> GetAcoesFrequentes()
    {
        var grouped = await _db.AuditLogs
            .AsNoTracking()
            .GroupBy(al => al.Acao)
            .Select(g => new { acao = g.Key, quantidade = g.Count() })
            .OrderByDescending(x => x.quantidade)
            .Take(8)
            .ToListAsync();

        var total = grouped.Sum(x => x.quantidade);

        var result = grouped.Select(x => new
        {
            x.acao,
            x.quantidade,
            percentagem = total > 0
                ? Math.Round((double)x.quantidade / total * 100, 1)
                : 0,
        });

        return Ok(result);
    }

    [HttpGet("atividades/atividade-recente")]
    public async Task<IActionResult> GetAtividadeRecente([FromQuery] int limite = 20)
    {
        limite = Math.Clamp(limite, 1, 100);

        var logs = await _db.AuditLogs
            .AsNoTracking()
            .Include(al => al.Admin)
            .OrderByDescending(al => al.Timestamp)
            .Take(limite)
            .ToListAsync();

        var result = logs.Select(al =>
        {
            var entidade = al.Acao switch
            {
                var a when a.Contains("USER") => "Utilizadores",
                var a when a.Contains("SESSION") => "Sessões",
                var a when a.Contains("VIAGEM") => "Viagens",
                var a when a.Contains("FATURA") => "Faturas",
                _ => "Sistema",
            };

            string detalhe = string.Empty;
            if (!string.IsNullOrEmpty(al.Detalhe))
            {
                try
                {
                    System.Text.Json.JsonDocument.Parse(al.Detalhe);
                    detalhe = al.Detalhe.Length > 80 ? al.Detalhe[..80] + "…" : al.Detalhe;
                }
                catch { detalhe = al.Detalhe.Length > 80 ? al.Detalhe[..80] + "…" : al.Detalhe; }
            }

            return new
            {
                id = al.Id,
                usuarioNome = al.Admin?.Nome ?? "Sistema",
                usuarioEmail = al.Admin?.Email ?? "",
                acao = al.Acao,
                entidade,
                detalhe,
                timestamp = al.Timestamp,
            };
        });

        return Ok(result);
    }

    [HttpGet("sessoes")]
    public async Task<IActionResult> GetSessoesAtivas()
    {
        var sessoes = await _sessaoService.GetTodasSessoesAtivasAsync();

        var result = sessoes.Select(s => new
        {
            id = s.SessionId,
            s.UserId,
            usuarioNome = s.User?.Nome ?? "Desconhecido",
            usuarioEmail = s.User?.Email ?? "",
            ip = s.IpAddress,
            s.UserAgent,
            ultimaAtividade = s.UltimaAtividade,
            dataCriacao = s.DataCriacao,
            dataExpiracao = s.DataExpiracao,
            tempoAtivo = DateTimeOffset.UtcNow - s.UltimaAtividade,
        });

        return Ok(result);
    }

    [HttpPost("sessoes/{sessionId}/terminar")]
    public async Task<IActionResult> TerminarSessao(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { message = "SessionId inválido." });

        var adminId = User.GetUserId();
        var sessao = await _db.Sessoes
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

        if (sessao == null)
            return NotFound(new { message = "Sessão não encontrada ou já terminada." });

        await _sessaoService.TerminarSessaoAsync(sessionId);

        await _audit.LogAsync(adminId, "SESSION_TERMINATED", new
        {
            SessionId = sessionId,
            TargetUserId = sessao.UserId,
            TerminatedBy = adminId,
        }, GetClientIp());

        return Ok(new { message = "Sessão terminada com sucesso." });
    }

    [HttpPost("sessoes/usuario/{userId}/terminar-todas")]
    public async Task<IActionResult> TerminarTodasSessoesUsuario(
        int userId,
        [FromBody] TerminarSessoesRequest? request)
    {
        var adminId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { message = "Utilizador não encontrado." });

        await _sessaoService.TerminarTodasSessoesUsuarioAsync(userId, request?.ExcludeSessionId);

        await _audit.LogAsync(adminId, "ALL_SESSIONS_TERMINATED", new
        {
            UserId = userId,
            UserEmail = user.Email,
            TerminatedBy = adminId,
        }, GetClientIp());

        return Ok(new { message = $"Todas as sessões de {user.Nome} foram terminadas." });
    }

    /// <summary>
    /// APENAS disponível em ambiente de desenvolvimento.
    /// Retorna credenciais do seed para facilitar testes.
    /// </summary>
    [HttpGet("seed-credentials")]
    [AllowAnonymous]
    public IActionResult GetSeedCredentials()
    {
        // Block completely in production — never expose default credentials
        if (!_env.IsDevelopment())
            return NotFound();

        return Ok(new
        {
            message = "Credenciais do administrador padrão (APENAS ambiente de desenvolvimento).",
            developmentCredentials = new
            {
                email = "admin@accusoft.com",
                password = "Admin123!",
                note = "Criadas automaticamente quando NÃO existe nenhum administrador na BD.",
            },
            instruction = "Após o primeiro login, altere a senha do administrador.",
        });
    }

    private string? GetClientIp() =>
        HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').First().Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    private static UserDto MapUserDto(User u) => new(
        u.Id, u.Nome, u.Email,
        u.Role.ToApiString(),
        u.Status.ToApiString(),
        u.Departamento, u.Cargo, u.Telefone, u.AvatarUrl,
        u.DataCriacao, u.UltimoLogin);

    public class TerminarSessoesRequest
    {
        public int? ExcludeSessionId { get; set; }
    }
}
