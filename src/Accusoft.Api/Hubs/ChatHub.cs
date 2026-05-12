// Hubs/ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Accusoft.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private static readonly Dictionary<string, int> _connections = new();

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _connections[Context.ConnectionId] = userId.Value;
            
            // Notificar outros usuários que este usuário está online
            await Clients.All.SendAsync("UserOnline", userId.Value);
            
            // Atualizar status do usuário
            await UpdateUserStatus(userId.Value, true);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryGetValue(Context.ConnectionId, out var userId))
        {
            _connections.Remove(Context.ConnectionId);
            
            // Notificar que o usuário ficou offline
            await Clients.All.SendAsync("UserOffline", userId);
            
            // Atualizar status
            await UpdateUserStatus(userId, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(int toUserId, string message)
    {
        var fromUserId = GetUserId();
        if (!fromUserId.HasValue) return;

        // Salvar mensagem no banco
        var chatMessage = new ChatMessage
        {
            FromUserId = fromUserId.Value,
            ToUserId = toUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        // Buscar nomes dos usuários
        var fromUser = await _db.Users.FindAsync(fromUserId.Value);
        var toUser = await _db.Users.FindAsync(toUserId);

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

        // Enviar para o destinatário se estiver online
        var recipientConnection = _connections.FirstOrDefault(x => x.Value == toUserId).Key;
        if (recipientConnection != null)
        {
            await Clients.Client(recipientConnection).SendAsync("ReceiveMessage", messageDto);
            
            // Marcar como lida se recebida em tempo real
            chatMessage.IsRead = true;
            await _db.SaveChangesAsync();
            
            // Notificar remetente que foi lida
            await Clients.Caller.SendAsync("MessageRead", chatMessage.Id);
        }

        // Enviar confirmação para o remetente
        await Clients.Caller.SendAsync("MessageSent", messageDto);
        
        // Atualizar contador de não lidas do destinatário
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
            .Where(m => m.FromUserId == fromUserId && m.ToUserId == currentUserId && !m.IsRead)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsRead = true;
        }

        await _db.SaveChangesAsync();
        
        await Clients.User(fromUserId.ToString()).SendAsync("MessagesRead", currentUserId);
    }

    public async Task GetConversations()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

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
                lastMessage = g.First().Message,
                lastMessageTime = g.First().CreatedAt,
                unreadCount = g.Count(m => m.ToUserId == userId && !m.IsRead)
            })
            .OrderByDescending(c => c.lastMessageTime)
            .ToList();

        await Clients.Caller.SendAsync("ConversationsLoaded", result);
    }

    public async Task GetConversation(int withUserId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var messages = await _db.ChatMessages
            .Where(m => (m.FromUserId == userId && m.ToUserId == withUserId) ||
                       (m.FromUserId == withUserId && m.ToUserId == userId))
            .OrderBy(m => m.CreatedAt)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .ToListAsync();

        // Marcar mensagens como lidas
        var unreadMessages = messages.Where(m => m.ToUserId == userId && !m.IsRead);
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
            m.CreatedAt
        });

        await Clients.Caller.SendAsync("ConversationLoaded", result);
    }

    public async Task GetUnreadCount()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var count = await _db.ChatMessages
            .CountAsync(m => m.ToUserId == userId && !m.IsRead);

        await Clients.Caller.SendAsync("UnreadCount", count);
    }

    private int? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out var userId))
            return userId;
        return null;
    }

    private async Task UpdateUserStatus(int userId, bool isOnline)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            // Você pode adicionar uma coluna "IsOnline" na tabela Users se quiser
            // Por enquanto, vamos apenas notificar via SignalR
        }
    }
}