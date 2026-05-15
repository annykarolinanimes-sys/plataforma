using System.IO;
using System.Security.Cryptography;
using Accusoft.Api.Data;
using Accusoft.Api.Domain.Entities;
using Accusoft.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accusoft.Api.Services;

public sealed class IntegridadeService : IIntegridadeService
{
    private readonly AppDbContext _context;
    private readonly EcmStorageOptions _storageOptions;
    private readonly ILogger<IntegridadeService> _logger;

    public IntegridadeService(
        AppDbContext context,
        IOptions<EcmStorageOptions> storageOptions,
        ILogger<IntegridadeService> logger)
    {
        _context = context;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<string> CalcularHashAsync(Stream stream, CancellationToken ct = default)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        stream.Seek(0, SeekOrigin.Begin);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<string> CalcularHashFicheiroAsync(string caminhoFisico, CancellationToken ct = default)
    {
        await using var stream = new FileStream(caminhoFisico, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return await CalcularHashAsync(stream, ct);
    }

    public async Task PersistirDocumentoComAcidAsync(
        Documento documento,
        Stream stream,
        string caminhoAbsoluto,
        Guid correlationId,
        CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var ficheiroEscrito = false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminhoAbsoluto)!);
            await using var fs = new FileStream(caminhoAbsoluto, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await stream.CopyToAsync(fs, 81920, ct);
            await fs.FlushAsync(ct);
            ficheiroEscrito = true;

            await _context.Documentos.AddAsync(documento, ct);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (ficheiroEscrito && File.Exists(caminhoAbsoluto))
            {
                try { File.Delete(caminhoAbsoluto); }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx,
                        "Falha ao limpar ficheiro após erro de persistência. Path={Path} CorrelationId={CorrelationId}",
                        caminhoAbsoluto, correlationId);
                }
            }

            _logger.LogError(ex,
                "Erro ao persistir documento em transação ACID. CorrelationId={CorrelationId}",
                correlationId);
            throw new EcmPersistenciaException("Erro ao persistir o documento.", ex);
        }
    }

    public async Task<ResultadoVerificacaoIntegridade> VerificarIntegridadeAsync(
        Guid documentoId,
        string caminhoFisico,
        string verificadoPor,
        Guid correlationId,
        CancellationToken ct = default)
    {
        var resultado = await CalcularHashFicheiroAsync(caminhoFisico, ct);
        var documento = await _context.Documentos
            .Where(d => d.Id == documentoId)
            .FirstOrDefaultAsync(ct);

        if (documento is null)
            throw new EcmPersistenciaException($"Documento '{documentoId}' não encontrado para verificação de integridade.");

        var igual = string.Equals(resultado, documento.HashSHA256, StringComparison.OrdinalIgnoreCase);
        if (!igual)
        {
            documento.IntegridadeVerificada = false;
            documento.UltimaVerificacaoIntegridade = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            return new ResultadoVerificacaoIntegridade(documentoId, false, documento.HashSHA256, resultado, DateTimeOffset.UtcNow,
                "Hash atual não coincide com o esperado.");
        }

        documento.IntegridadeVerificada = true;
        documento.UltimaVerificacaoIntegridade = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return new ResultadoVerificacaoIntegridade(documentoId, true, documento.HashSHA256, resultado, DateTimeOffset.UtcNow, null);
    }
}