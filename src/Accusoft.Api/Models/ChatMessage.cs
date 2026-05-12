using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("chat_messages")]
public class ChatMessage
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("from_user_id")]
    public int FromUserId { get; set; }

    [ForeignKey(nameof(FromUserId))]
    public User FromUser { get; set; } = null!;

    [Column("to_user_id")]
    public int ToUserId { get; set; }

    [ForeignKey(nameof(ToUserId))]
    public User ToUser { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}