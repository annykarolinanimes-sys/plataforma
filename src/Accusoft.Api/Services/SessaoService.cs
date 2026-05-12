// Services/SessaoService.cs
using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Services;

public class SessaoService : ISessaoService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SessaoService> _logger;

    public SessaoService(AppDbContext db, ILogger<SessaoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Sessao> CriarSessaoAsync(string sessionId, int userId, string tokenJwt, string? ipAddress, string? userAgent, DateTime expiracao)
    {
        var sessao = new Sessao
        {
            SessionId = sessionId,
            UserId = userId,
            TokenJwt = tokenJwt,
            IpAddress = ipAddress,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
            DataCriacao = DateTimeOffset.UtcNow,
            UltimaAtividade = DateTimeOffset.UtcNow,
            DataExpiracao = expiracao,
            IsActive = true
        };

        _db.Sessoes.Add(sessao);
        await _db.SaveChangesAsync();
        
        return sessao;
    }

    public async Task<bool> ValidarSessaoAsync(string sessionId)
    {
        var sessao = await _db.Sessoes
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

        if (sessao == null)
            return false;

        if (sessao.DataExpiracao < DateTimeOffset.UtcNow)
        {
            sessao.IsActive = false;
            await _db.SaveChangesAsync();
            return false;
        }

        return true;
    }

    public async Task AtualizarAtividadeAsync(string sessionId)
    {
        await _db.Sessoes
            .Where(s => s.SessionId == sessionId && s.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.UltimaAtividade, DateTimeOffset.UtcNow));
    }

    public async Task TerminarSessaoAsync(string sessionId)
    {
        await _db.Sessoes
            .Where(s => s.SessionId == sessionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.IsActive, false));
                
        _logger.LogInformation("Sessão {SessionId} terminada", sessionId);
    }

    public async Task TerminarTodasSessoesUsuarioAsync(int userId, int? excludeSessionId = null)
    {
        var query = _db.Sessoes.Where(s => s.UserId == userId && s.IsActive);
        
        if (excludeSessionId.HasValue)
        {
            query = query.Where(s => s.Id != excludeSessionId.Value);
        }
        
        await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.IsActive, false));
            
        _logger.LogInformation("Todas as sessões do utilizador {UserId} terminadas", userId);
    }

    public async Task<List<Sessao>> GetSessoesAtivasByUserAsync(int userId)
    {
        return await _db.Sessoes
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.UserId == userId && s.IsActive && s.DataExpiracao > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.UltimaAtividade)
            .ToListAsync();
    }

    public async Task<List<Sessao>> GetTodasSessoesAtivasAsync()
    {
        return await _db.Sessoes
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.IsActive && s.DataExpiracao > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.UltimaAtividade)
            .ToListAsync();
    }

    public async Task LimparSessoesExpiradasAsync()
    {
        var expiradas = await _db.Sessoes
            .Where(s => s.IsActive && s.DataExpiracao < DateTimeOffset.UtcNow)
            .ToListAsync();
            
        foreach (var sessao in expiradas)
        {
            sessao.IsActive = false;
        }
        
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Count} sessões expiradas limpas", expiradas.Count);
    }
}