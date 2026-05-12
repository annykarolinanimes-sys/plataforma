using System.Text.Json;
using Accusoft.Api.Data;
using Accusoft.Api.Models;

namespace Accusoft.Api.Services;

public interface IAuditService
{
    Task LogAsync(int adminId, string acao, object? detalhe = null, string? ip = null);
}

public class AuditService(AppDbContext db) : IAuditService
{
    public async Task LogAsync(int adminId, string acao, object? detalhe = null, string? ip = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            AdminId   = adminId,
            Acao      = acao,
            Detalhe   = detalhe is not null ? JsonSerializer.Serialize(detalhe) : null,
            IpAddress = ip,
            Timestamp = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}