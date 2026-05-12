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
    private readonly ISessaoService _sessaoService;  // ← Adicionar

    // ← Modificar construtor
    public AdminController(AppDbContext db, IAuditService audit, ISessaoService sessaoService)
    {
        _db = db;
        _audit = audit;
        _sessaoService = sessaoService;
    }
    
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search)
    {
        var query = _db.Users.AsNoTracking();

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
        var target  = await _db.Users.FindAsync(req.UserId);

        if (target is null)
            return NotFound(new { message = "Utilizador não encontrado." });

        if (target.Id == adminId)
            return BadRequest(new { message = "Não pode desativar a sua própria conta." });

        var estadoAnterior = target.Status;

        target.Status = target.Status == UserStatus.Ativo
            ? UserStatus.Inativo
            : UserStatus.Ativo;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(adminId, "USER_TOGGLE", new
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
        var totalAtivos     = await _db.Users.CountAsync(u => u.Status == UserStatus.Ativo);
        var totalInativos   = await _db.Users.CountAsync(u => u.Status == UserStatus.Inativo);
        var totalAlertas    = await _db.Alertas.CountAsync();
        var alertasNaoLidos = await _db.Alertas.CountAsync(a => !a.Lido);

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
        var query = _db.AuditLogs
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

    [HttpGet("seed-credentials")]
    [AllowAnonymous]
    public IActionResult GetSeedCredentials()
    {
        return Ok(new
        {
            message = "Credenciais do administrador padrão (se não existir nenhum admin no sistema)",
            developmentCredentials = new
            {
                email = "admin@accusoft.com",
                password = "Admin123",
                note = "Estas credenciais só são criadas automaticamente quando NÃO existe nenhum administrador no banco de dados."
            },
            instruction = "Após o primeiro login, altere a senha do administrador para maior segurança."
        });
    }

    // ─── ENDPOINTS DE SESSÃO (CORRIGIDOS) ─────────────────────────────────────

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
            s.IpAddress,
            s.UserAgent,
            ultimaAtividade = s.UltimaAtividade,
            dataCriacao = s.DataCriacao,
            dataExpiracao = s.DataExpiracao,
            tempoAtivo = DateTimeOffset.UtcNow - s.UltimaAtividade
        });
        
        return Ok(result);
    }

    [HttpPost("sessoes/{sessionId}/terminar")]
    public async Task<IActionResult> TerminarSessao(string sessionId)
    {
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
            TerminatedBy = adminId
        }, GetClientIp());
        
        return Ok(new { message = "Sessão terminada com sucesso." });
    }

    [HttpPost("sessoes/usuario/{userId}/terminar-todas")]
    public async Task<IActionResult> TerminarTodasSessoesUsuario(int userId, [FromBody] TerminarSessoesRequest? request)
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
            TerminatedBy = adminId
        }, GetClientIp());
        
        return Ok(new { message = $"Todas as sessões de {user.Nome} foram terminadas." });
    }

    public class TerminarSessoesRequest
    {
        public int? ExcludeSessionId { get; set; }
    }
}