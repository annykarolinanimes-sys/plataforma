using Accusoft.Api.Domain.Enums;
using System.Collections.ObjectModel;

namespace Accusoft.Api.Domain.Entities;

public partial class Documento
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string NomeOriginal { get; private set; } = string.Empty;
    public string NomeFisico { get; private set; } = string.Empty;
    public string CaminhoRelativo { get; private set; } = string.Empty;
    public string Extensao { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public string HashSHA256 { get; private set; } = string.Empty;
    public DateTimeOffset? HashCalculadoEm { get; private set; }
    public string MimeTypeValidado { get; private set; } = string.Empty;

    public CategoriaDocumento Categoria { get; private set; }
    public ContextoDocumento Contexto { get; private set; }
    public Guid? EntidadeAssociadaId { get; private set; }

    public string? CaminhoFisico { get; private set; }
    public string? Descricao { get; private set; }
    public EstadoDocumento Estado { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Workflow
    public string? EstadoAlteradoPor { get; private set; }
    public DateTimeOffset? EstadoAlteradoEm { get; private set; }
    public string? RazaoEstado { get; private set; }

    // Segurança
    public bool StorageImutavel { get; private set; }
    public bool ScanAntivirusRealizado { get; private set; }
    public ResultadoScan? ResultadoScanAntivirus { get; private set; }
    public DateTimeOffset? ScanAntivirusEm { get; private set; }

    public bool Validado { get; private set; }
    public string? ValidadoPor { get; private set; }
    public DateTimeOffset? ValidadoEm { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool Encriptado { get; private set; }
    public bool RetencaoLegalAtiva { get; private set; }
    public DateTimeOffset? DataExpiracaoRetencao { get; private set; }

    public bool? IntegridadeVerificada { get; internal set; }
    public DateTimeOffset? UltimaVerificacaoIntegridade { get; internal set; }

    private readonly List<DocumentoHistorico> _historico = new();
    public virtual IReadOnlyCollection<DocumentoHistorico> Historico => _historico.AsReadOnly();

    protected Documento() { }

    public static Documento Criar(
        Guid tenantId,
        Guid correlationId,
        string nomeOriginal,
        string nomeFisico,
        string caminhoRelativo,
        long tamanhoBytes,
        string mimeTypeValidado,
        string extensao,
        string hashSHA256,
        CategoriaDocumento categoria,
        ContextoDocumento contexto,
        string criadoPor,
        Guid? entidadeAssociadaId = null,
        string? descricao = null)
    {
        return new Documento
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CorrelationId = correlationId,
            NomeOriginal = nomeOriginal,
            NomeFisico = nomeFisico,
            CaminhoRelativo = caminhoRelativo,
            TamanhoBytes = tamanhoBytes,
            MimeTypeValidado = mimeTypeValidado,
            Extensao = extensao.ToLowerInvariant().TrimStart('.'),
            HashSHA256 = hashSHA256,
            Categoria = categoria,
            Contexto = contexto,
            EntidadeAssociadaId = entidadeAssociadaId,
            Descricao = descricao,
            Estado = EstadoDocumento.Em_Analise,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = criadoPor
        };
    }
}