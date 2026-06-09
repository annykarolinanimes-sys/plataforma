using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Accusoft.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatHub> _logger;

    // ConcurrentDictionary is thread-safe — the original Dictionary was not
    private static readonly ConcurrentDictionary<string, int> _connections = new();

    public ChatHub(AppDbContext db, ILogger<ChatHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _connections[Context.ConnectionId] = userId.Value;
            await Clients.Others.SendAsync("UserOnline", userId.Value);
            _logger.LogDebug("User {UserId} connected via SignalR. ConnectionId={ConnectionId}",
                userId.Value, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var userId))
        {
            // Only notify offline if no other connections exist for this user
            var stillConnected = _connections.Values.Any(v => v == userId);
            if (!stillConnected)
                await Clients.Others.SendAsync("UserOffline", userId);

            _logger.LogDebug("User {UserId} disconnected from SignalR. ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        if (exception is not null)
            _logger.LogWarning(exception, "SignalR disconnected with error. ConnectionId={ConnectionId}",
                Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(int toUserId, string message)
    {
        var fromUserId = GetUserId();
        if (!fromUserId.HasValue) return;

        if (toUserId <= 0)
        {
            await Clients.Caller.SendAsync("Error", "Destinatário inválido.");
            return;
        }

        // Validate and sanitize message
        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("Error", "Mensagem não pode ser vazia.");
            return;
        }

        // Limit message length
        if (message.Length > 4000)
            message = message[..4000];

        // Verify recipient exists
        var toUserExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == toUserId && u.Status == UserStatus.Ativo);

        if (!toUserExists)
        {
            await Clients.Caller.SendAsync("Error", "Destinatário não encontrado.");
            return;
        }

        var chatMessage = new ChatMessage
        {
            FromUserId = fromUserId.Value,
            ToUserId = toUserId,
            Message = message.Trim(),
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        var fromUser = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Nome })
            .FirstOrDefaultAsync(u => u.Id == fromUserId.Value);

        var toUser = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Nome })
            .FirstOrDefaultAsync(u => u.Id == toUserId);

        var messageDto = new
        {
            id = chatMessage.Id,
            fromUserId = chatMessage.FromUserId,
            fromUserName = fromUser?.Nome ?? "Desconhecido",
            toUserId = chatMessage.ToUserId,
            toUserName = toUser?.Nome ?? "Desconhecido",
            message = chatMessage.Message,
            isRead = chatMessage.IsRead,
            createdAt = chatMessage.CreatedAt
        };

        // Send to recipient if connected
        var recipientConnection = _connections
            .Where(x => x.Value == toUserId)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (recipientConnection is not null)
        {
            await Clients.Client(recipientConnection).SendAsync("ReceiveMessage", messageDto);

            // Mark as read since recipient is online
            chatMessage.IsRead = true;
            await _db.SaveChangesAsync();
            await Clients.Caller.SendAsync("MessageRead", chatMessage.Id);
        }

        await Clients.Caller.SendAsync("MessageSent", messageDto);
        await Clients.User(toUserId.ToString()).SendAsync("UpdateUnreadCount");
    }

    public async Task MarkAsRead(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message != null && message.ToUserId == userId.Value && !message.IsRead)
        {
            message.IsRead = true;
            await _db.SaveChangesAsync();
            await Clients.User(message.FromUserId.ToString()).SendAsync("MessageRead", messageId);
        }
    }

    public async Task MarkAllAsRead(int fromUserId)
    {
        var currentUserId = GetUserId();
        if (!currentUserId.HasValue) return;

        var messages = await _db.ChatMessages
            .Where(m => m.FromUserId == fromUserId &&
                        m.ToUserId == currentUserId.Value &&
                        !m.IsRead)
            .ToListAsync();

        if (messages.Count == 0) return;

        foreach (var msg in messages)
            msg.IsRead = true;

        await _db.SaveChangesAsync();
        await Clients.User(fromUserId.ToString()).SendAsync("MessagesRead", currentUserId);
    }

    public async Task GetConversations()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var conversations = await _db.ChatMessages
            .Where(m => m.FromUserId == userId.Value || m.ToUserId == userId.Value)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var result = conversations
            .GroupBy(m => m.FromUserId == userId.Value ? m.ToUserId : m.FromUserId)
            .Select(g => new
            {
                userId = g.Key,
                userName = g.First().FromUserId == userId.Value
                    ? g.First().ToUser.Nome
                    : g.First().FromUser.Nome,
                lastMessage = g.First().Message,
                lastMessageTime = g.First().CreatedAt,
                unreadCount = g.Count(m => m.ToUserId == userId.Value && !m.IsRead)
            })
            .OrderByDescending(c => c.lastMessageTime)
            .ToList();

        await Clients.Caller.SendAsync("ConversationsLoaded", result);
    }

    public async Task GetConversation(int withUserId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        if (withUserId <= 0) return;

        var messages = await _db.ChatMessages
            .Where(m => (m.FromUserId == userId.Value && m.ToUserId == withUserId) ||
                        (m.FromUserId == withUserId && m.ToUserId == userId.Value))
            .OrderBy(m => m.CreatedAt)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .ToListAsync();

        // Mark incoming messages as read
        var unreadMessages = messages
            .Where(m => m.ToUserId == userId.Value && !m.IsRead)
            .ToList();

        if (unreadMessages.Count > 0)
        {
            foreach (var msg in unreadMessages)
                msg.IsRead = true;
            await _db.SaveChangesAsync();
        }

        var result = messages.Select(m => new
        {
            m.Id,
            m.FromUserId,
            fromUserName = m.FromUser.Nome,
            m.ToUserId,
            toUserName = m.ToUser.Nome,
            m.Message,
            m.IsRead,
            m.CreatedAt
        });

        await Clients.Caller.SendAsync("ConversationLoaded", result);
    }

    public async Task GetUnreadCount()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var count = await _db.ChatMessages
            .CountAsync(m => m.ToUserId == userId.Value && !m.IsRead);

        await Clients.Caller.SendAsync("UnreadCount", count);
    }

    private int? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("userId")?.Value
                          ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
