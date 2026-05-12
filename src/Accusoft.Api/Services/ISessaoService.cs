// Services/ISessaoService.cs
using Accusoft.Api.Models;

namespace Accusoft.Api.Services;

public interface ISessaoService
{
    Task<Sessao> CriarSessaoAsync(string sessionId, int userId, string tokenJwt, string? ipAddress, string? userAgent, DateTime expiracao);
    Task<bool> ValidarSessaoAsync(string sessionId);
    Task AtualizarAtividadeAsync(string sessionId);
    Task TerminarSessaoAsync(string sessionId);
    Task TerminarTodasSessoesUsuarioAsync(int userId, int? excludeSessionId = null);
    Task<List<Sessao>> GetSessoesAtivasByUserAsync(int userId);
    Task<List<Sessao>> GetTodasSessoesAtivasAsync();
    Task LimparSessoesExpiradasAsync();
}