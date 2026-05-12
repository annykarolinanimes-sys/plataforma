// Models/Sessao.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accusoft.Api.Models;

[Table("sessoes")]
public class Sessao
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("session_id"), MaxLength(200)]
    public string SessionId { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Column("token_jwt"), MaxLength(500)]
    public string? TokenJwt { get; set; }

    [Column("ip_address"), MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent"), MaxLength(500)]
    public string? UserAgent { get; set; }

    [Column("data_criacao")]
    public DateTimeOffset DataCriacao { get; set; } = DateTimeOffset.UtcNow;

    [Column("ultima_atividade")]
    public DateTimeOffset UltimaAtividade { get; set; } = DateTimeOffset.UtcNow;

    [Column("data_expiracao")]
    public DateTimeOffset DataExpiracao { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}