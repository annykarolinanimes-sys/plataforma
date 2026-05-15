using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Accusoft.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDocumentoService _documentoService;

    public DocumentosController(AppDbContext context, IDocumentoService documentoService)
    {
        _context = context;
        _documentoService = documentoService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocumentos([FromQuery] string? tipo, [FromQuery] string? search)
    {
        var query = _context.Documentos.AsQueryable();

        if (!string.IsNullOrEmpty(tipo))
        {
            query = query.Where(d => d.Categoria.ToString() == tipo);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.NomeOriginal.Contains(search));
        }

        var documentos = await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentoListDto(
                d.Id,
                d.NomeOriginal,
                d.CaminhoRelativo,
                d.Categoria.ToString(),
                d.TamanhoBytes,
                FormatBytes(d.TamanhoBytes),
                d.CreatedBy,
                d.CreatedAt,
                d.Estado))
            .ToListAsync();

        return Ok(documentos);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadDocumento(Guid id)
    {
        var documento = await _context.Documentos.FindAsync(id);
        if (documento == null)
        {
            return NotFound();
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", documento.CaminhoRelativo.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var fileName = $"{documento.NomeOriginal}";
        return File(fileBytes, documento.MimeTypeValidado, fileName);
    }

    /// <summary>
    /// Endpoint para upload de Blobs gerados (PDFs de relatórios, faturas, etc).
    /// Persiste o arquivo no ECM com metadados estruturados.
    /// Retorna: {documentoId, nomeOriginal, hashSHA256, url para download}
    /// </summary>
    [HttpPost("upload-stream")]
    [Produces(typeof(UploadDocumentoResponse))]
    public async Task<IActionResult> UploadStream(
        [FromForm] IFormFile ficheiro,
        [FromForm] string categoria = "Relatorio",
        [FromForm] string contexto = "Interno",
        [FromForm] string? descricao = null,
        CancellationToken ct = default)
    {
        try
        {
            // Extrair identidade do JWT
            var userId = User.GetUserId().ToString();
            var tenantId = Guid.NewGuid(); // TODO: extrair do JWT se multi-tenant está em uso

            // Construir comando de upload
            var command = new UploadDocumentoCommand(
                Ficheiro: ficheiro,
                Categoria: Enum.Parse<CategoriaDocumento>(categoria),
                Contexto: Enum.Parse<ContextoDocumento>(contexto),
                EntidadeAssociadaId: null,
                Descricao: descricao ?? $"Documento gerado em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            );

            // Executar upload via serviço (já maneja validação, hash, persistência ACID)
            var result = await _documentoService.UploadAsync(
                command,
                tenantId,
                userId,
                Guid.NewGuid(), // CorrelationId para rastreio
                ct
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new { erro = result.Erro, tipo = result.TipoErro });
            }

            // Retornar response enriquecida com URL download
            var response = result.Value!;
            var downloadUrl = $"/api/documentos/{response.DocumentoId}/download";

            return Ok(new
            {
                response.DocumentoId,
                response.NomeOriginal,
                response.HashSHA256,
                response.MimeTypeDetectado,
                response.TamanhoBytes,
                response.Versao,
                response.Estado,
                response.CorrelationId,
                response.Mensagem,
                Url = downloadUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = $"Erro ao fazer upload: {ex.Message}" });
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        var units = new[] { "B", "KB", "MB", "GB" };
        var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
        return $"{bytes / Math.Pow(1024, i):F1} {units[i]}";
    }

    /// <summary>
    /// Upload de um novo documento
    /// </summary>
    [HttpPost]
    [Produces(typeof(UploadDocumentoResponse))]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile ficheiro,
        [FromForm] string categoria = "Relatorio",
        [FromForm] string contexto = "Interno",
        [FromForm] string? descricao = null,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.GetUserId().ToString();
            var tenantId = Guid.NewGuid(); // TODO: extrair do JWT se multi-tenant está em uso

            var command = new UploadDocumentoCommand(
                Ficheiro: ficheiro,
                Categoria: Enum.Parse<CategoriaDocumento>(categoria),
                Contexto: Enum.Parse<ContextoDocumento>(contexto),
                EntidadeAssociadaId: null,
                Descricao: descricao ?? $"Documento enviado em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            );

            var result = await _documentoService.UploadAsync(
                command,
                tenantId,
                userId,
                Guid.NewGuid(),
                ct
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new { erro = result.Erro, tipo = result.TipoErro });
            }

            var response = result.Value!;
            var downloadUrl = $"/api/documentos/{response.DocumentoId}/download";

            return Ok(new
            {
                response.DocumentoId,
                response.NomeOriginal,
                response.HashSHA256,
                response.MimeTypeDetectado,
                response.TamanhoBytes,
                response.Versao,
                response.Estado,
                response.CorrelationId,
                response.Mensagem,
                Url = downloadUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = $"Erro ao fazer upload: {ex.Message}" });
        }
    }

    /// <summary>
    /// Criar nova versão de um documento existente
    /// </summary>
    [HttpPost("{documentoId}/versoes")]
    [Produces(typeof(UploadDocumentoResponse))]
    public async Task<IActionResult> NovaVersao(
        Guid documentoId,
        [FromForm] IFormFile ficheiro,
        [FromForm] string? descricao = null,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.GetUserId().ToString();
            var tenantId = Guid.NewGuid(); // TODO: extrair do JWT se multi-tenant está em uso

            var command = new NovaVersaoDocumentoCommand(
                DocumentoId: documentoId,
                Ficheiro: ficheiro,
                Descricao: descricao ?? $"Nova versão criada em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            );

            var result = await _documentoService.NovaVersaoAsync(
                command,
                tenantId,
                userId,
                Guid.NewGuid(),
                ct
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new { erro = result.Erro, tipo = result.TipoErro });
            }

            var response = result.Value!;
            var downloadUrl = $"/api/documentos/{response.DocumentoId}/download";

            return Ok(new
            {
                response.DocumentoId,
                response.NomeOriginal,
                response.HashSHA256,
                response.MimeTypeDetectado,
                response.TamanhoBytes,
                response.Versao,
                response.Estado,
                response.CorrelationId,
                response.Mensagem,
                Url = downloadUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = $"Erro ao criar nova versão: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obter histórico de operações de um documento
    /// </summary>
    [HttpGet("{documentoId}/historico")]
    [Produces(typeof(IReadOnlyList<DocumentoHistoricoResponse>))]
    public async Task<IActionResult> ObterHistorico(Guid documentoId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = Guid.NewGuid(); // TODO: extrair do JWT se multi-tenant está em uso
            var result = await _documentoService.ObterHistoricoAsync(documentoId, tenantId, ct);

            if (!result.IsSuccess)
            {
                return BadRequest(new { erro = result.Erro, tipo = result.TipoErro });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = $"Erro ao obter histórico: {ex.Message}" });
        }
    }
}