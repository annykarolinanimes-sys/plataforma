using Accusoft.Api.Domain.Enums;

namespace Accusoft.Api.Domain.Entities;

/// <summary>
/// Extensão de versioning e workflow da entidade Documento.
/// Partial class — complementa Documento.cs sem o modificar.
///
/// Cadeia de versões:
///   v1 (IsLatest=false) ← v2 (IsLatest=false) ← v3 (IsLatest=true)
///   Todas partilham o mesmo DocumentoOrigemId (= Id da v1).
/// </summary>
public partial class Documento
{
    // ─── Versionamento ────────────────────────────────────────────────────────

    /// <summary>Número de versão (começa em 1, incrementa por nova versão).</summary>
    public int Versao { get; private set; } = 1;

    /// <summary>
    /// Indica se esta é a versão mais recente do documento.
    /// Apenas uma versão por cadeia pode ter IsLatest = true.
    /// </summary>
    public bool IsLatest { get; private set; } = true;

    /// <summary>
    /// ID do documento original (v1) que iniciou a cadeia de versões.
    /// Null se este documento for a v1 (raiz da cadeia).
    /// </summary>
    public Guid? DocumentoOrigemId { get; private set; }

    /// <summary>
    /// Referência ao documento da versão anterior.
    /// Null se for a primeira versão.
    /// </summary>
    public Guid? VersaoAnteriorId { get; private set; }

    // ─── Workflow — Rejeição ──────────────────────────────────────────────────

    /// <summary>Comentário de rejeição (obrigatório quando rejeitado por Admin).</summary>
    public string? ComentarioRejeicao { get; private set; }

    /// <summary>Utilizador Admin que rejeitou o documento.</summary>
    public string? RejeitadoPor { get; private set; }

    /// <summary>Data/hora da rejeição (UTC).</summary>
    public DateTimeOffset? RejeitadoEm { get; private set; }

    // ─── Navegação de Versões ─────────────────────────────────────────────────

    /// <summary>Documento raiz da cadeia (navegação EF Core).</summary>
    public Documento? DocumentoOrigem { get; private set; }

    /// <summary>Versões mais recentes que derivam deste documento.</summary>
    public IReadOnlyCollection<Documento> VersoesDerivadas => _versoesDerivadas.AsReadOnly();
    private readonly List<Documento> _versoesDerivadas = new();

    // ─── Factory Method — Nova Versão ─────────────────────────────────────────

    /// <summary>
    /// Cria uma nova versão deste documento.
    /// Responsabilidade do DocumentoService:
    ///   1. Chamar este método na versão atual
    ///   2. Chamar MarcarComoSupersedida() nesta instância
    ///   3. Persistir ambas na mesma transação ACID
    /// </summary>
    public Documento CriarNovaVersao(
        Guid correlationId,
        string nomeOriginal,
        string nomeFisico,
        string caminhoRelativo,
        long tamanhoBytes,
        string mimeTypeValidado,
        string extensao,
        string hashSHA256,
        string criadoPor)
    {
        if (!IsLatest)
            throw new InvalidOperationException(
                "Não é possível criar nova versão a partir de uma versão não-atual.");

        if (StorageImutavel)
            throw new InvalidOperationException(
                "Documento com storage imutável (WORM). Novas versões não são permitidas.");

        var agora = DateTimeOffset.UtcNow;
        var origemId = DocumentoOrigemId ?? Id; // Se sou v1, o meu ID é a origem

        var novaVersao = new Documento
        {
            TenantId = TenantId,
            CorrelationId = correlationId,
            NomeOriginal = nomeOriginal,
            NomeFisico = nomeFisico,
            CaminhoRelativo = caminhoRelativo,
            TamanhoBytes = tamanhoBytes,
            MimeTypeValidado = mimeTypeValidado,
            Extensao = extensao.ToLowerInvariant().TrimStart('.'),
            HashSHA256 = hashSHA256,
            HashCalculadoEm = agora,
            Categoria = Categoria,
            Contexto = Contexto,
            EntidadeAssociadaId = EntidadeAssociadaId,
            Descricao = Descricao,
            Estado = EstadoDocumento.Em_Analise,
            Versao = Versao + 1,
            IsLatest = true,
            DocumentoOrigemId = origemId,
            VersaoAnteriorId = Id,
            CreatedAt = agora,
            CreatedBy = criadoPor
        };

        novaVersao._historico.Add(DocumentoHistorico.Criar(
            novaVersao.Id,
            TipoOperacaoHistorico.Upload,
            criadoPor,
            correlationId,
            $"Nova versão v{novaVersao.Versao} criada a partir de v{Versao}. " +
            $"SHA-256: {hashSHA256[..16]}…"));

        return novaVersao;
    }

    /// <summary>
    /// Marca este documento como supersedido por uma nova versão.
    /// Chamado em conjunto com CriarNovaVersao() na mesma transação.
    /// </summary>
    public void MarcarComoSupersedida(Guid novaVersaoId, string operadoPor, Guid correlationId)
    {
        IsLatest = false;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = operadoPor;

        _historico.Add(DocumentoHistorico.Criar(
            Id,
            TipoOperacaoHistorico.TransicaoEstado,
            operadoPor,
            correlationId,
            $"Versão v{Versao} supersedida pela nova versão. NovaVersaoId={novaVersaoId}",
            estadoAnterior: "IsLatest=true",
            estadoPosterior: "IsLatest=false"));
    }

    /// <summary>
    /// Marca um documento como validado.
    /// </summary>
    public void Validar(string validadoPor, Guid correlationId)
    {
        if (Estado is EstadoDocumento.Eliminado or EstadoDocumento.Arquivado)
            throw new InvalidOperationException(
                $"Documento no estado '{Estado}' não pode ser validado.");

        if (Validado)
            throw new InvalidOperationException("Documento já está validado.");

        Validado = true;
        ValidadoPor = validadoPor;
        ValidadoEm = DateTimeOffset.UtcNow;
        Estado = EstadoDocumento.Ativo;
        UpdatedBy = validadoPor;
        UpdatedAt = DateTimeOffset.UtcNow;

        _historico.Add(DocumentoHistorico.Criar(
            Id,
            TipoOperacaoHistorico.Validacao,
            validadoPor,
            correlationId,
            "Documento validado.",
            estadoAnterior: EstadoDocumento.Em_Analise.ToString(),
            estadoPosterior: Estado.ToString()));
    }

    /// <summary>
    /// Marca o documento como eliminado logicamente.
    /// </summary>
    public void EliminarLogicamente(string operadoPor, Guid correlationId, string razao)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Documento já está eliminado.");

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        RazaoEstado = razao;
        Estado = EstadoDocumento.Eliminado;
        UpdatedBy = operadoPor;
        UpdatedAt = DateTimeOffset.UtcNow;

        _historico.Add(DocumentoHistorico.Criar(
            Id,
            TipoOperacaoHistorico.SoftDelete,
            operadoPor,
            correlationId,
            $"Documento eliminado logicamente. Razão: {razao}",
            estadoAnterior: EstadoDocumento.Ativo.ToString(),
            estadoPosterior: Estado.ToString()));
    }

    // ─── Métodos de Workflow ───────────────────────────────────────────────────

    /// <summary>
    /// Rejeição por Admin (RBAC: Documento.Validate).
    /// Comentário obrigatório para compliance.
    /// </summary>
    public void Rejeitar(string comentario, string rejeitadoPor, Guid correlationId)
    {
        if (Estado is EstadoDocumento.Eliminado or EstadoDocumento.Arquivado)
            throw new InvalidOperationException(
                $"Documento no estado '{Estado}' não pode ser rejeitado.");

        ArgumentException.ThrowIfNullOrWhiteSpace(comentario,
            "Comentário de rejeição é obrigatório para compliance.");

        var estadoAnterior = Estado.ToString();
        Estado = EstadoDocumento.Em_Analise; // Volta a Em_Analise para reavaliação
        ComentarioRejeicao = comentario;
        RejeitadoPor = rejeitadoPor;
        RejeitadoEm = DateTimeOffset.UtcNow;
        EstadoAlteradoPor = rejeitadoPor;
        EstadoAlteradoEm = DateTimeOffset.UtcNow;
        RazaoEstado = $"Rejeitado: {comentario}";

        _historico.Add(DocumentoHistorico.Criar(
            Id,
            TipoOperacaoHistorico.TransicaoEstado,
            rejeitadoPor,
            correlationId,
            $"Documento rejeitado. Comentário: {comentario}",
            estadoAnterior: estadoAnterior,
            estadoPosterior: Estado.ToString()));
    }

    /// <summary>
    /// Arquivo de longo prazo (Admin). Documenta encerramento do ciclo de vida ativo.
    /// </summary>
    public void Arquivar(string motivo, string operadoPor, Guid correlationId)
    {
        if (Estado is EstadoDocumento.Eliminado or EstadoDocumento.Arquivado)
            throw new InvalidOperationException(
                $"Documento no estado '{Estado}' não pode ser arquivado.");

        var estadoAnterior = Estado.ToString();
        Estado = EstadoDocumento.Arquivado;
        RazaoEstado = motivo;
        EstadoAlteradoPor = operadoPor;
        EstadoAlteradoEm = DateTimeOffset.UtcNow;

        _historico.Add(DocumentoHistorico.Criar(
            Id,
            TipoOperacaoHistorico.Arquivamento,
            operadoPor,
            correlationId,
            $"Documento arquivado. Motivo: {motivo}",
            estadoAnterior: estadoAnterior,
            estadoPosterior: Estado.ToString()));
    }
}
