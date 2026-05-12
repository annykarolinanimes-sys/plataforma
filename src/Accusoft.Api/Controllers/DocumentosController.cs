using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
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

    public DocumentosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocumentos([FromQuery] string? tipo, [FromQuery] string? search)
    {
        var query = _context.Documentos.AsQueryable();

        if (!string.IsNullOrEmpty(tipo))
        {
            query = query.Where(d => d.Tipo.ToString() == tipo);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.Nome.Contains(search));
        }

        var documentos = await query
            .OrderByDescending(d => d.DataUpload)
            .Select(d => new DTOs.DocumentoDto(
                d.Id,
                d.Nome,
                d.PathUrl,
                d.Tipo.ToString(),
                d.TamanhoBytes,
                FormatBytes(d.TamanhoBytes),
                d.UsuarioId,
                d.EnvioId,
                d.DataUpload,
                d.DataAbertura))
            .ToListAsync();

        return Ok(documentos);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadDocumento(int id)
    {
        var documento = await _context.Documentos.FindAsync(id);
        if (documento == null)
        {
            return NotFound();
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", documento.PathUrl.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var fileName = $"{documento.Nome}.pdf";
        return File(fileBytes, "application/pdf", fileName);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        var units = new[] { "B", "KB", "MB", "GB" };
        var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
        return $"{bytes / Math.Pow(1024, i):F1} {units[i]}";
    }
}