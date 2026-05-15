using Accusoft.Api.DTOs;
using Accusoft.Api.Domain.Entities;
using Accusoft.Api.Domain.Enums;
using Accusoft.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accusoft.Api.Services;

public interface IDocumentoService
{
    Task<Result<UploadDocumentoResponse>> UploadAsync(
        UploadDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<UploadDocumentoResponse>> UploadFromStreamAsync(
        UploadDocumentoStreamCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<UploadDocumentoResponse>> NovaVersaoAsync(
        NovaVersaoDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<PagedResult<DocumentoResumoResponse>>> ListarAsync(
        ListarDocumentosQuery query, Guid tenantId, CancellationToken ct = default);

    Task<Result<DocumentoResponse>> ObterAsync(
        Guid id, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DocumentoHistoricoResponse>>> ObterHistoricoAsync(
        Guid id, Guid tenantId, CancellationToken ct = default);

    Task<Result<DocumentoResponse>> ValidarAsync(
        ValidarDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<DocumentoResponse>> RejeitarAsync(
        RejeitarDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<DocumentoResponse>> ArquivarAsync(
        ArquivarDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<bool>> EliminarAsync(
        EliminarDocumentoCommand command, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);

    Task<Result<DocumentoDownloadInfo>> ObterInfoDownloadAsync(
        Guid id, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default);
}

/// <summary>Informação necessária para o controller construir o FileStreamResult.</summary>
public sealed record DocumentoDownloadInfo(
    Guid DocumentoId,
    string CaminhoAbsoluto,
    string NomeOriginal,
    string MimeType,
    long TamanhoBytes,
    string HashSHA256
);

// ═════════════════════════════════════════════════════════════════════════════
// Configuração
// ═════════════════════════════════════════════════════════════════════════════

public sealed class EcmStorageOptions
{
    public string RaizStorage { get; set; } = "/var/ecm/storage";
    public long TamanhoMaximoBytes { get; set; } = 50 * 1024 * 1024; // 50 MB default
}

// ═════════════════════════════════════════════════════════════════════════════
// Implementação
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Serviço central do módulo ECM.
/// Orquestra: MIME sniffing → cálculo SHA-256 → persistência ACID →
///            versionamento → histórico → auditoria.
/// </summary>
public sealed class DocumentoService : IDocumentoService
{
    private readonly AppDbContext _context;
    private readonly IMimeSniffingService _mimeSniffing;
    private readonly IIntegridadeService _integridade;
    private readonly EcmStorageOptions _storageOptions;
    private readonly ILogger<DocumentoService> _logger;

    public DocumentoService(
        AppDbContext context,
        IMimeSniffingService mimeSniffing,
        IIntegridadeService integridade,
        IOptions<EcmStorageOptions> storageOptions,
        ILogger<DocumentoService> logger)
    {
        _context = context;
        _mimeSniffing = mimeSniffing;
        _integridade = integridade;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    // ─── Upload ───────────────────────────────────────────────────────────────

    public async Task<Result<UploadDocumentoResponse>> UploadAsync(
        UploadDocumentoCommand command,
        Guid tenantId,
        string userId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Upload iniciado. Ficheiro={Nome} Tenant={TenantId} CorrelationId={CorrelationId}",
            command.Ficheiro.FileName, tenantId, correlationId);

        // ── 1. Validações básicas ─────────────────────────────────────────────
        if (command.Ficheiro.Length == 0)
            return Result<UploadDocumentoResponse>.Falha("O ficheiro está vazio.");

        if (command.Ficheiro.Length > _storageOptions.TamanhoMaximoBytes)
            return Result<UploadDocumentoResponse>.Falha(
                $"Ficheiro excede o tamanho máximo de {_storageOptions.TamanhoMaximoBytes / 1024 / 1024} MB.");

        // ── 2. MIME Sniffing (Magic Numbers) ──────────────────────────────────
        await using var stream = command.Ficheiro.OpenReadStream();
        var resultadoMime = await _mimeSniffing.AnalisarAsync(stream, command.Ficheiro.FileName, ct);

        if (!resultadoMime.Permitido)
        {
            _logger.LogWarning(
                "Upload bloqueado por MIME sniffing. Motivo={Motivo} CorrelationId={CorrelationId}",
                resultadoMime.MotivoBloqueo, correlationId);

            return Result<UploadDocumentoResponse>.Falha(
                $"Ficheiro rejeitado: {resultadoMime.MotivoBloqueo}");
        }

        // Alertar sobre conflito (extensão ≠ magic bytes) mas permitir se MIME é válido
        if (resultadoMime.MimeConflito)
        {
            _logger.LogWarning(
                "Conflito MIME detectado mas permitido. Extensao declarada={MimeDeclarado} " +
                "Magic bytes={MimeReal} CorrelationId={CorrelationId}",
                resultadoMime.MimeTypeDeclarado, resultadoMime.MimeTypeDetectado, correlationId);
        }

        // ── 3. Calcular SHA-256 via streaming ─────────────────────────────────
        stream.Seek(0, SeekOrigin.Begin);
        var hashSHA256 = await _integridade.CalcularHashAsync(stream, ct);

        // ── 4. Calcular caminho físico seguro ─────────────────────────────────
        var nomeFisico = GerarNomeFisico(command.Ficheiro.FileName);
        var (caminhoRelativo, caminhoAbsoluto) = CalcularCaminhos(tenantId, nomeFisico);

        // ── 5. Construir entidade de domínio ──────────────────────────────────
        var documento = Documento.Criar(
            tenantId: tenantId,
            correlationId: correlationId,
            nomeOriginal: SanitizarNome(command.Ficheiro.FileName),
            nomeFisico: nomeFisico,
            caminhoRelativo: caminhoRelativo,
            tamanhoBytes: command.Ficheiro.Length,
            mimeTypeValidado: resultadoMime.MimeTypeDetectado,
            extensao: Path.GetExtension(command.Ficheiro.FileName),
            hashSHA256: hashSHA256,
            categoria: command.Categoria,
            contexto: command.Contexto,
            criadoPor: userId,
            entidadeAssociadaId: command.EntidadeAssociadaId,
            descricao: command.Descricao);

        // ── 6. Persistência ACID (ficheiro + BD na mesma transação lógica) ────
        stream.Seek(0, SeekOrigin.Begin);

        await _integridade.PersistirDocumentoComAcidAsync(
            documento, stream, caminhoAbsoluto, correlationId, ct);

        _logger.LogInformation(
            "Upload concluído com sucesso. DocumentoId={Id} Hash={Hash} CorrelationId={CorrelationId}",
            documento.Id, hashSHA256[..16], correlationId);

        return Result<UploadDocumentoResponse>.Ok(new UploadDocumentoResponse(
            DocumentoId: documento.Id,
            NomeOriginal: documento.NomeOriginal,
            HashSHA256: hashSHA256,
            MimeTypeDetectado: resultadoMime.MimeTypeDetectado,
            TamanhoBytes: documento.TamanhoBytes,
            Versao: documento.Versao,
            Estado: documento.Estado,
            CorrelationId: correlationId,
            Mensagem: resultadoMime.MimeConflito
                ? "Documento submetido. Atenção: conflito de tipo detetado — aguarda revisão."
                : "Documento submetido com sucesso. Em análise."));
    }

    public async Task<Result<UploadDocumentoResponse>> UploadFromStreamAsync(
        UploadDocumentoStreamCommand command,
        Guid tenantId,
        string userId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (command.FicheiroStream is null)
            return Result<UploadDocumentoResponse>.Falha("Stream de ficheiro não fornecida.");

        var stream = command.FicheiroStream;
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 81920, ct);
            buffer.Position = 0;
            stream = buffer;
        }

        stream.Seek(0, SeekOrigin.Begin);
        var tamanhoBytes = command.TamanhoBytes > 0 ? command.TamanhoBytes : stream.Length;

        var ficheiro = new FormFile(stream, 0, tamanhoBytes, "ficheiro", command.NomeOriginal)
        {
            Headers = new HeaderDictionary(),
            ContentType = string.IsNullOrWhiteSpace(command.MimeType)
                ? "application/octet-stream"
                : command.MimeType,
        };

        var uploadCommand = new UploadDocumentoCommand(
            Ficheiro: ficheiro,
            Categoria: command.Categoria,
            Contexto: command.Contexto,
            EntidadeAssociadaId: command.EntidadeAssociadaId,
            Descricao: command.Descricao);

        return await UploadAsync(uploadCommand, tenantId, userId, correlationId, ct);
    }

    // ─── Nova Versão ──────────────────────────────────────────────────────────

    public async Task<Result<UploadDocumentoResponse>> NovaVersaoAsync(
        NovaVersaoDocumentoCommand command,
        Guid tenantId,
        string userId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        // ── 1. Obter documento atual (IsLatest = true) ────────────────────────
        var documentoAtual = await _context.Documentos
            .Where(d => d.Id == command.DocumentoId && d.TenantId == tenantId && d.IsLatest)
            .FirstOrDefaultAsync(ct);

        if (documentoAtual is null)
            return Result<UploadDocumentoResponse>.NaoEncontrado("Documento", command.DocumentoId);

        if (command.Ficheiro.Length > _storageOptions.TamanhoMaximoBytes)
            return Result<UploadDocumentoResponse>.Falha("Ficheiro excede o tamanho máximo permitido.");

        // ── 2. MIME sniffing ──────────────────────────────────────────────────
        await using var stream = command.Ficheiro.OpenReadStream();
        var resultadoMime = await _mimeSniffing.AnalisarAsync(stream, command.Ficheiro.FileName, ct);

        if (!resultadoMime.Permitido)
            return Result<UploadDocumentoResponse>.Falha(
                $"Nova versão rejeitada: {resultadoMime.MotivoBloqueo}");

        // ── 3. Hash SHA-256 ───────────────────────────────────────────────────
        stream.Seek(0, SeekOrigin.Begin);
        var hashSHA256 = await _integridade.CalcularHashAsync(stream, ct);

        // ── 4. Prevenir duplicação: mesma versão já existe? ───────────────────
        var hashJaExiste = await _context.Documentos
            .AnyAsync(d => d.HashSHA256 == hashSHA256
                        && d.DocumentoOrigemId == (documentoAtual.DocumentoOrigemId ?? documentoAtual.Id)
                        && d.TenantId == tenantId, ct);

        if (hashJaExiste)
            return Result<UploadDocumentoResponse>.Conflito(
                "Este ficheiro é idêntico a uma versão já existente (hash SHA-256 coincide).");

        // ── 5. Construir nova versão via domínio ──────────────────────────────
        var nomeFisico = GerarNomeFisico(command.Ficheiro.FileName);
        var (caminhoRelativo, caminhoAbsoluto) = CalcularCaminhos(tenantId, nomeFisico);

        var novaVersao = documentoAtual.CriarNovaVersao(
            correlationId: correlationId,
            nomeOriginal: SanitizarNome(command.Ficheiro.FileName),
            nomeFisico: nomeFisico,
            caminhoRelativo: caminhoRelativo,
            tamanhoBytes: command.Ficheiro.Length,
            mimeTypeValidado: resultadoMime.MimeTypeDetectado,
            extensao: Path.GetExtension(command.Ficheiro.FileName),
            hashSHA256: hashSHA256,
            criadoPor: userId);

        // ── 6. Superseder a versão atual e persistir ambas na mesma transação ─
        documentoAtual.MarcarComoSupersedida(novaVersao.Id, userId, correlationId);

        stream.Seek(0, SeekOrigin.Begin);

        // Usar transação manual para coordenar update da versão antiga + insert da nova
        await using var transacao = await _context.Database.BeginTransactionAsync(ct);
        var ficheiroEscrito = false;

        try
        {
            await EscreverFicheiroAsync(stream, caminhoAbsoluto, ct);
            ficheiroEscrito = true;

            _context.Documentos.Update(documentoAtual);
            await _context.Documentos.AddAsync(novaVersao, ct);
            await _context.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        catch when (ficheiroEscrito)
        {
            await transacao.RollbackAsync(CancellationToken.None);
            TentarEliminarFicheiro(caminhoAbsoluto, correlationId);
            throw;
        }
        catch
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }

        _logger.LogInformation(
            "Nova versão v{Versao} criada. DocumentoId={Id} OrigemId={OrigemId} CorrelationId={CorrelationId}",
            novaVersao.Versao, novaVersao.Id, novaVersao.DocumentoOrigemId, correlationId);

        return Result<UploadDocumentoResponse>.Ok(new UploadDocumentoResponse(
            DocumentoId: novaVersao.Id,
            NomeOriginal: novaVersao.NomeOriginal,
            HashSHA256: hashSHA256,
            MimeTypeDetectado: resultadoMime.MimeTypeDetectado,
            TamanhoBytes: novaVersao.TamanhoBytes,
            Versao: novaVersao.Versao,
            Estado: novaVersao.Estado,
            CorrelationId: correlationId,
            Mensagem: $"Versão v{novaVersao.Versao} criada com sucesso."));
    }

    // ─── Listagem Paginada ────────────────────────────────────────────────────

    public async Task<Result<PagedResult<DocumentoResumoResponse>>> ListarAsync(
        ListarDocumentosQuery query,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var q = _context.Documentos
            .Where(d => d.TenantId == tenantId)
            .AsNoTracking();

        if (query.ApenasLatest == true)
            q = q.Where(d => d.IsLatest);

        if (query.Estado.HasValue)
            q = q.Where(d => d.Estado == query.Estado.Value);

        if (query.Categoria.HasValue)
            q = q.Where(d => d.Categoria == query.Categoria.Value);

        if (query.Contexto.HasValue)
            q = q.Where(d => d.Contexto == query.Contexto.Value);

        if (query.EntidadeAssociadaId.HasValue)
            q = q.Where(d => d.EntidadeAssociadaId == query.EntidadeAssociadaId.Value);

        if (!string.IsNullOrWhiteSpace(query.PesquisaNome))
            q = q.Where(d => d.NomeOriginal.Contains(query.PesquisaNome));

        if (query.CriadoApos.HasValue)
            q = q.Where(d => d.CreatedAt >= query.CriadoApos.Value);

        if (query.CriadoAntes.HasValue)
            q = q.Where(d => d.CreatedAt <= query.CriadoAntes.Value);

        var total = await q.CountAsync(ct);
        var pagina = Math.Max(1, query.Pagina);
        var tamanho = Math.Clamp(query.TamanhoPagina, 1, 100);

        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(d => new DocumentoResumoResponse(
                d.Id, d.NomeOriginal, d.MimeTypeValidado,
                FormatarBytes(d.TamanhoBytes),
                d.Estado, d.Categoria, d.Contexto,
                d.Versao, d.IsLatest, d.Validado,
                d.IntegridadeVerificada ?? false,
                d.CreatedAt, d.CreatedBy))
            .ToListAsync(ct);

        return Result<PagedResult<DocumentoResumoResponse>>.Ok(new PagedResult<DocumentoResumoResponse>
        {
            Items    = items,
            Total    = total,
            Page     = pagina,
            PageSize = tamanho
        });
    }

    // ─── Obter Detalhe ────────────────────────────────────────────────────────

    public async Task<Result<DocumentoResponse>> ObterAsync(
        Guid id, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        var documento = await _context.Documentos
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (documento is null)
            return Result<DocumentoResponse>.NaoEncontrado("Documento", id);

        // Registar acesso no histórico (RBAC: Documento.View)
        var historico = DocumentoHistorico.Criar(
            id, TipoOperacaoHistorico.Visualizacao, userId, correlationId,
            $"Documento consultado. Estado: {documento.Estado}");

        _context.DocumentosHistorico.Add(historico);
        await _context.SaveChangesAsync(ct);

        return Result<DocumentoResponse>.Ok(MapearParaResponse(documento));
    }

    // ─── Histórico ────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DocumentoHistoricoResponse>>> ObterHistoricoAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var existe = await _context.Documentos
            .AnyAsync(d => d.Id == id && d.TenantId == tenantId, ct);

        if (!existe)
            return Result<IReadOnlyList<DocumentoHistoricoResponse>>.NaoEncontrado("Documento", id);

        var historico = await _context.DocumentosHistorico
            .Where(h => h.DocumentoId == id)
            .OrderByDescending(h => h.OcorridoEm)
            .AsNoTracking()
            .Select(h => new DocumentoHistoricoResponse(
                h.Id, h.TipoOperacao, h.Descricao, h.ExecutadoPor,
                h.IpOrigem, h.EstadoAnterior, h.EstadoPosterior,
                h.CorrelationId, h.OcorridoEm))
            .ToListAsync(ct);

        return Result<IReadOnlyList<DocumentoHistoricoResponse>>.Ok(historico);
    }

    // ─── Workflow: Validar ────────────────────────────────────────────────────

    public async Task<Result<DocumentoResponse>> ValidarAsync(
        ValidarDocumentoCommand command,
        Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        var documento = await ObterDocumentoTracked(command.DocumentoId, tenantId, ct);
        if (documento is null)
            return Result<DocumentoResponse>.NaoEncontrado("Documento", command.DocumentoId);

        try
        {
            documento.Validar(userId, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentoResponse>.Falha(ex.Message);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Documento validado. Id={Id} Por={User} CorrelationId={CorrelationId}",
            command.DocumentoId, userId, correlationId);

        return Result<DocumentoResponse>.Ok(MapearParaResponse(documento));
    }

    // ─── Workflow: Rejeitar ───────────────────────────────────────────────────

    public async Task<Result<DocumentoResponse>> RejeitarAsync(
        RejeitarDocumentoCommand command,
        Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Comentario))
            return Result<DocumentoResponse>.Falha("Comentário de rejeição é obrigatório.");

        var documento = await ObterDocumentoTracked(command.DocumentoId, tenantId, ct);
        if (documento is null)
            return Result<DocumentoResponse>.NaoEncontrado("Documento", command.DocumentoId);

        try
        {
            documento.Rejeitar(command.Comentario, userId, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentoResponse>.Falha(ex.Message);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Documento rejeitado. Id={Id} Por={User} Comentario={Comentario} CorrelationId={CorrelationId}",
            command.DocumentoId, userId, command.Comentario, correlationId);

        return Result<DocumentoResponse>.Ok(MapearParaResponse(documento));
    }

    // ─── Workflow: Arquivar ───────────────────────────────────────────────────

    public async Task<Result<DocumentoResponse>> ArquivarAsync(
        ArquivarDocumentoCommand command,
        Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        var documento = await ObterDocumentoTracked(command.DocumentoId, tenantId, ct);
        if (documento is null)
            return Result<DocumentoResponse>.NaoEncontrado("Documento", command.DocumentoId);

        try
        {
            documento.Arquivar(command.Motivo, userId, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentoResponse>.Falha(ex.Message);
        }

        await _context.SaveChangesAsync(ct);
        return Result<DocumentoResponse>.Ok(MapearParaResponse(documento));
    }

    // ─── Soft Delete ──────────────────────────────────────────────────────────

    public async Task<Result<bool>> EliminarAsync(
        EliminarDocumentoCommand command,
        Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        var documento = await ObterDocumentoTracked(command.DocumentoId, tenantId, ct);
        if (documento is null)
            return Result<bool>.NaoEncontrado("Documento", command.DocumentoId);

        try
        {
            documento.EliminarLogicamente(userId, correlationId, command.Razao);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Falha(ex.Message);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Soft delete executado. Id={Id} Por={User} Razao={Razao} CorrelationId={CorrelationId}",
            command.DocumentoId, userId, command.Razao, correlationId);

        return Result<bool>.Ok(true);
    }

    // ─── Info para Download Seguro ────────────────────────────────────────────

    public async Task<Result<DocumentoDownloadInfo>> ObterInfoDownloadAsync(
        Guid id, Guid tenantId, string userId, Guid correlationId, CancellationToken ct = default)
    {
        var documento = await _context.Documentos
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (documento is null)
            return Result<DocumentoDownloadInfo>.NaoEncontrado("Documento", id);

        if (documento.Estado is EstadoDocumento.Quarentena or EstadoDocumento.IntegridadeCompromissada)
            return Result<DocumentoDownloadInfo>.AcessoNegado(
                $"Download bloqueado. Documento em estado: {documento.Estado}");

        if (documento.Estado == EstadoDocumento.Eliminado)
            return Result<DocumentoDownloadInfo>.NaoEncontrado("Documento", id);

        var caminhoAbsoluto = Path.Combine(_storageOptions.RaizStorage, documento.CaminhoRelativo);

        // Registar acesso
        _context.DocumentosHistorico.Add(DocumentoHistorico.Criar(
            id, TipoOperacaoHistorico.Download, userId, correlationId,
            $"Download solicitado. Ficheiro: {documento.NomeFisico}"));
        await _context.SaveChangesAsync(ct);

        return Result<DocumentoDownloadInfo>.Ok(new DocumentoDownloadInfo(
            DocumentoId: id,
            CaminhoAbsoluto: caminhoAbsoluto,
            NomeOriginal: documento.NomeOriginal,
            MimeType: documento.MimeTypeValidado,
            TamanhoBytes: documento.TamanhoBytes,
            HashSHA256: documento.HashSHA256));
    }

    // ─── Helpers Privados ─────────────────────────────────────────────────────

    private async Task<Documento?> ObterDocumentoTracked(Guid id, Guid tenantId, CancellationToken ct)
        => await _context.Documentos
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

    private (string caminhoRelativo, string caminhoAbsoluto) CalcularCaminhos(Guid tenantId, string nomeFisico)
    {
        // Estrutura: {tenant}/{ano}/{mes}/{nomeFisico}
        var agora = DateTimeOffset.UtcNow;
        var relativo = Path.Combine(
            tenantId.ToString("N"),
            agora.Year.ToString(),
            agora.Month.ToString("D2"),
            nomeFisico);
        var absoluto = Path.Combine(_storageOptions.RaizStorage, relativo);
        return (relativo, absoluto);
    }

    private static string GerarNomeFisico(string nomeOriginal)
    {
        var ext = Path.GetExtension(nomeOriginal).ToLowerInvariant();
        // Apenas extensões alfanuméricas — previne injeção via extensão
        ext = System.Text.RegularExpressions.Regex.IsMatch(ext, @"^\.[a-z0-9]{1,10}$")
            ? ext : string.Empty;
        return $"{Guid.NewGuid():N}{ext}";
    }

    private static string SanitizarNome(string nome)
        => Path.GetFileName(nome).Trim(); // Strip path traversal

    private static async Task EscreverFicheiroAsync(Stream stream, string destino, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using var fs = new FileStream(destino, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(fs, 81920, ct);
        await fs.FlushAsync(ct);
    }

    private void TentarEliminarFicheiro(string caminho, Guid correlationId)
    {
        try { if (File.Exists(caminho)) File.Delete(caminho); }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "ROLLBACK FÍSICO FALHOU. Ficheiro órfão: {Caminho} CorrelationId={CorrelationId}",
                caminho, correlationId);
        }
    }

    private static string FormatarBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private static DocumentoResponse MapearParaResponse(Documento d) => new(
        d.Id, d.TenantId, d.CorrelationId, d.NomeOriginal,
        d.TamanhoBytes, FormatarBytes(d.TamanhoBytes),
        d.MimeTypeValidado, d.Extensao, d.HashSHA256, d.HashCalculadoEm ?? d.CreatedAt,
        d.Categoria, d.Contexto, d.EntidadeAssociadaId, d.Descricao,
        d.Estado, d.RazaoEstado,
        d.ScanAntivirusRealizado, d.ResultadoScanAntivirus,
        d.Encriptado, d.RetencaoLegalAtiva, d.DataExpiracaoRetencao,
        d.IsDeleted, d.DeletedAt, d.Validado, d.ValidadoPor, d.ValidadoEm,
        d.ComentarioRejeicao, d.RejeitadoPor, d.RejeitadoEm,
        d.Versao, d.IsLatest, d.DocumentoOrigemId, d.VersaoAnteriorId,
        d.IntegridadeVerificada ?? false, d.UltimaVerificacaoIntegridade,
        d.CreatedBy, d.CreatedAt, d.UpdatedBy, d.UpdatedAt);
}
