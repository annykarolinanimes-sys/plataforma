using Accusoft.Api.Domain.Enums;

namespace Accusoft.Api.Domain.Entities;

/// <summary>
/// Registo imutável de operações realizadas num documento.
/// Cada entrada é um snapshot do momento da operação.
/// </summary>
public class DocumentoHistorico
{
    public Guid Id { get; private set; }
    public Guid DocumentoId { get; private set; }
    public TipoOperacaoHistorico TipoOperacao { get; private set; }
    public string OperadoPor { get; private set; } = string.Empty;
    public string ExecutadoPor => OperadoPor;
    public string? IpOrigem { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public string? EstadoAnterior { get; private set; }
    public string? EstadoPosterior { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public DateTimeOffset OcorridoEm => Timestamp;

    // Navegação EF Core
    public virtual Documento Documento { get; private set; } = null!;

    protected DocumentoHistorico() { }

    public static DocumentoHistorico Criar(
        Guid documentoId,
        TipoOperacaoHistorico tipo,
        string operadoPor,
        Guid correlationId,
        string descricao,
        string? ipOrigem = null,
        string? estadoAnterior = null,
        string? estadoPosterior = null)
    {
        return new DocumentoHistorico
        {
            Id = Guid.NewGuid(),
            DocumentoId = documentoId,
            TipoOperacao = tipo,
            OperadoPor = operadoPor,
            IpOrigem = ipOrigem,
            CorrelationId = correlationId,
            Descricao = descricao,
            EstadoAnterior = estadoAnterior,
            EstadoPosterior = estadoPosterior,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}