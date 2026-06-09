using System.Text.Json;
using Accusoft.Api.Data;
using Accusoft.Api.Models;

namespace Accusoft.Api.Services;

public interface IAuditService
{
    Task LogAsync(int adminId, string acao, object? detalhe = null, string? ip = null);
}

public class AuditService(AppDbContext db, ILogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task LogAsync(int adminId, string acao, object? detalhe = null, string? ip = null)
    {
        if (adminId <= 0)
        {
            logger.LogWarning("AuditService.LogAsync called with invalid adminId={AdminId}", adminId);
            return;
        }

        if (string.IsNullOrWhiteSpace(acao))
        {
            logger.LogWarning("AuditService.LogAsync called with empty acao");
            return;
        }

        // Truncate acao to column limit
        var acaoTruncated = acao.Length > 100 ? acao[..100] : acao;

        string? detalheJson = null;
        if (detalhe is not null)
        {
            try
            {
                detalheJson = JsonSerializer.Serialize(detalhe, _jsonOptions);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to serialize audit detail for action {Acao}", acao);
                detalheJson = "{\"error\":\"serialization_failed\"}";
            }
        }

        // Sanitise IP
        var ipTruncated = ip?.Length > 45 ? ip[..45] : ip;

        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                AdminId   = adminId,
                Acao      = acaoTruncated,
                Detalhe   = detalheJson,
                IpAddress = ipTruncated,
                Timestamp = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit failure must NEVER crash the application
            logger.LogError(ex,
                "Failed to persist audit log. AdminId={AdminId} Acao={Acao}", adminId, acao);
        }
    }
}
