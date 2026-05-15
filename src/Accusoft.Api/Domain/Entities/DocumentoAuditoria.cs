namespace Accusoft.Api.Domain.Entities;

/// <summary>
/// Registo de auditoria de requests do módulo ECM.
/// </summary>
public sealed class DocumentoAuditoria
{
    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string IpOrigem { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string MetodoHttp { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string Acao { get; private set; } = string.Empty;
    public int HttpStatusCode { get; private set; }
    public long TempoRespostaMs { get; private set; }
    public Guid? DocumentoId { get; private set; }
    public string? UtilizadorId { get; private set; }
    public string? UtilizadorEmail { get; private set; }
    public string? RolesJson { get; private set; }
    public string? MensagemErro { get; private set; }
    public long TamanhoRespostaBytes { get; private set; }
    public DateTimeOffset RegistadoEm { get; private set; }

    public Documento? Documento { get; private set; }

    private DocumentoAuditoria() { }

    public static DocumentoAuditoria Criar(
        Guid correlationId,
        Guid tenantId,
        string ipOrigem,
        string userAgent,
        string metodoHttp,
        string endpoint,
        string acao,
        int httpStatusCode,
        long tempoRespostaMs,
        Guid? documentoId,
        string? utilizadorId,
        string? utilizadorEmail,
        string? rolesJson,
        string? mensagemErro,
        long tamanhoRespostaBytes)
    {
        return new DocumentoAuditoria
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            TenantId = tenantId,
            IpOrigem = ipOrigem,
            UserAgent = userAgent,
            MetodoHttp = metodoHttp,
            Endpoint = endpoint,
            Acao = acao,
            HttpStatusCode = httpStatusCode,
            TempoRespostaMs = tempoRespostaMs,
            DocumentoId = documentoId,
            UtilizadorId = utilizadorId,
            UtilizadorEmail = utilizadorEmail,
            RolesJson = rolesJson,
            MensagemErro = mensagemErro,
            TamanhoRespostaBytes = tamanhoRespostaBytes,
            RegistadoEm = DateTimeOffset.UtcNow
        };
    }
}
