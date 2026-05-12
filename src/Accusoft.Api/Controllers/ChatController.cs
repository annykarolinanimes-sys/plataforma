using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Accusoft.Api.Extensions;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChatController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search)
    {
        var currentUserId = User.GetUserId();
        
        var query = _db.Users
            .Where(u => u.Id != currentUserId && u.Status == UserStatus.Ativo)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => 
                u.Nome.ToLower().Contains(search.ToLower()) ||
                (u.Email != null && u.Email.ToLower().Contains(search.ToLower())));
        }

        var users = await query
            .Select(u => new
            {
                u.Id,
                u.Nome,
                u.Email,
                u.AvatarUrl,
                u.Departamento,
                u.Cargo
            })
            .OrderBy(u => u.Nome)
            .ToListAsync();

        // Adicionar informações de online status (opcional)
        // Você pode implementar isso com SignalR ou Redis

        return Ok(users);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.GetUserId();

        var conversations = await _db.ChatMessages
            .Where(m => m.FromUserId == userId || m.ToUserId == userId)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var result = conversations
            .GroupBy(m => m.FromUserId == userId ? m.ToUserId : m.FromUserId)
            .Select(g => new
            {
                userId = g.Key,
                userName = g.First().FromUserId == userId 
                    ? g.First().ToUser.Nome 
                    : g.First().FromUser.Nome,
                userEmail = g.First().FromUserId == userId 
                    ? g.First().ToUser.Email 
                    : g.First().FromUser.Email,
                userAvatar = g.First().FromUserId == userId 
                    ? g.First().ToUser.AvatarUrl 
                    : g.First().FromUser.AvatarUrl,
                lastMessage = g.First().Message,
                lastMessageTime = g.First().CreatedAt,
                unreadCount = g.Count(m => m.ToUserId == userId && !m.IsRead)
            })
            .OrderByDescending(c => c.lastMessageTime)
            .ToList();

        return Ok(result);
    }

    [HttpGet("messages/{userId}")]
    public async Task<IActionResult> GetMessages(int userId)
    {
        var currentUserId = User.GetUserId();

        var messages = await _db.ChatMessages
            .Where(m => (m.FromUserId == currentUserId && m.ToUserId == userId) ||
                       (m.FromUserId == userId && m.ToUserId == currentUserId))
            .OrderBy(m => m.CreatedAt)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .ToListAsync();

        // Marcar mensagens como lidas
        var unreadMessages = messages.Where(m => m.ToUserId == currentUserId && !m.IsRead);
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }
        await _db.SaveChangesAsync();

        var result = messages.Select(m => new
        {
            m.Id,
            m.FromUserId,
            fromUserName = m.FromUser.Nome,
            m.ToUserId,
            toUserName = m.ToUser.Nome,
            m.Message,
            m.IsRead,
            createdAt = m.CreatedAt
        });

        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserId();
        var count = await _db.ChatMessages
            .CountAsync(m => m.ToUserId == userId && !m.IsRead);
        
        return Ok(new { count });
    }

    [HttpPost("mark-read/{messageId}")]
    public async Task<IActionResult> MarkAsRead(int messageId)
    {
        var userId = User.GetUserId();
        var message = await _db.ChatMessages.FindAsync(messageId);
        
        if (message == null || message.ToUserId != userId)
            return NotFound();

        message.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}