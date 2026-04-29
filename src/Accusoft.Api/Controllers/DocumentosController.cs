using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/documentos-gerais")]
[Authorize]
public class DocumentosGeraisController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DocumentosGeraisController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ─── GET /api/user/documentos-gerais ──────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDocumentos(
        [FromQuery] string? tipo,
        [FromQuery] string? categoria,
        [FromQuery] string? search,
        [FromQuery] int? entidadeId,
        [FromQuery] string? entidadeRelacionada,
        [FromQuery] bool? favorito,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.DocumentosGerais!
            .AsNoTracking()
            .Where(d => d.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(d => d.Tipo == tipo);

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(d => d.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(d =>
                d.Nome.ToLower().Contains(s) ||
                d.NumeroDocumento.ToLower().Contains(s) ||
                (d.Descricao != null && d.Descricao.ToLower().Contains(s)) ||
                (d.Tags != null && d.Tags.ToLower().Contains(s)));
        }

        if (entidadeId.HasValue)
            query = query.Where(d => d.EntidadeId == entidadeId.Value);

        if (!string.IsNullOrWhiteSpace(entidadeRelacionada))
            query = query.Where(d => d.EntidadeRelacionada == entidadeRelacionada);

        if (favorito.HasValue)
            query = query.Where(d => d.Favorito == favorito.Value);

        if (dataInicio.HasValue)
            query = query.Where(d => d.DataDocumento >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(d => d.DataDocumento <= dataFim.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Buscar nomes das entidades relacionadas
        var entidadeNomes = await CarregarNomesEntidades(items);

        var result = new PagedResult<DocumentoGeralResponseDto>
        {
            Items = items.Select(d => MapToResponseDto(d, entidadeNomes)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/documentos-gerais/{id} ─────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDocumento(int id)
    {
        var uid = User.GetUserId();
        var documento = await _db.DocumentosGerais!
            .FirstOrDefaultAsync(d => d.Id == id && d.UsuarioId == uid);

        if (documento is null)
            return NotFound(new { message = "Documento não encontrado." });

        // Incrementar visualizações
        documento.Visualizacoes++;
        documento.UltimoAcesso = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var entidadeNomes = await CarregarNomesEntidades(new List<DocumentoGeral> { documento });
        return Ok(MapToResponseDto(documento, entidadeNomes));
    }

    // ─── POST /api/user/documentos-gerais/upload ──────────────────────────────
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB max
    public async Task<IActionResult> UploadDocumento([FromForm] DocumentoUploadDto dto)
    {
        if (dto.Ficheiro is null || dto.Ficheiro.Length == 0)
            return BadRequest(new { message = "Nenhum ficheiro enviado." });

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        // Validar tipo de ficheiro
        var extensao = Path.GetExtension(dto.Ficheiro.FileName).ToLower();
        var tiposPermitidos = new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".jpg", ".jpeg", ".png" };
        if (!tiposPermitidos.Contains(extensao))
            return BadRequest(new { message = "Tipo de ficheiro não permitido." });

        // Guardar ficheiro
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "documentos", uid.ToString());
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var nomeSeguro = $"{Guid.NewGuid()}{extensao}";
        var caminhoCompleto = Path.Combine(uploadsDir, nomeSeguro);
        var caminhoUrl = $"/uploads/documentos/{uid}/{nomeSeguro}";

        using (var stream = System.IO.File.Create(caminhoCompleto))
        {
            await dto.Ficheiro.CopyToAsync(stream);
        }

        var documento = new DocumentoGeral
        {
            NumeroDocumento = GerarNumeroDocumento(),
            Tipo = dto.Tipo,
            Nome = string.IsNullOrWhiteSpace(dto.Nome) ? dto.Ficheiro.FileName : dto.Nome,
            Descricao = dto.Descricao,
            DataDocumento = dto.DataDocumento ?? DateTime.UtcNow,
            DataCriacao = DateTime.UtcNow,
            CaminhoFicheiro = caminhoUrl,
            TamanhoBytes = dto.Ficheiro.Length,
            EntidadeRelacionada = dto.EntidadeRelacionada,
            EntidadeId = dto.EntidadeId,
            Tags = dto.Tags,
            Categoria = dto.Categoria,
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.DocumentosGerais!.Add(documento);
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(documento, new Dictionary<int, string>()));
    }

    // ─── PUT /api/user/documentos-gerais/{id} ─────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDocumento(int id, [FromBody] DocumentoGeralUpdateDto dto)
    {
        var uid = User.GetUserId();
        var documento = await _db.DocumentosGerais!
            .FirstOrDefaultAsync(d => d.Id == id && d.UsuarioId == uid);

        if (documento is null)
            return NotFound(new { message = "Documento não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nome))
            documento.Nome = dto.Nome;

        if (!string.IsNullOrWhiteSpace(dto.Descricao))
            documento.Descricao = dto.Descricao;

        if (!string.IsNullOrWhiteSpace(dto.Tags))
            documento.Tags = dto.Tags;

        if (!string.IsNullOrWhiteSpace(dto.Categoria))
            documento.Categoria = dto.Categoria;

        if (dto.Favorito.HasValue)
            documento.Favorito = dto.Favorito.Value;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            documento.Observacoes = dto.Observacoes;

        documento.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var entidadeNomes = await CarregarNomesEntidades(new List<DocumentoGeral> { documento });
        return Ok(MapToResponseDto(documento, entidadeNomes));
    }

    // ─── GET /api/user/documentos-gerais/{id}/download ────────────────────────
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadDocumento(int id)
    {
        var uid = User.GetUserId();
        var documento = await _db.DocumentosGerais!
            .FirstOrDefaultAsync(d => d.Id == id && d.UsuarioId == uid);

        if (documento is null)
            return NotFound(new { message = "Documento não encontrado." });

        if (string.IsNullOrEmpty(documento.CaminhoFicheiro))
            return NotFound(new { message = "Ficheiro não encontrado." });

        var caminhoCompleto = Path.Combine(_env.ContentRootPath, documento.CaminhoFicheiro.TrimStart('/'));
        if (!System.IO.File.Exists(caminhoCompleto))
            return NotFound(new { message = "Ficheiro não encontrado no servidor." });

        // Incrementar downloads
        documento.Downloads++;
        documento.UltimoAcesso = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var fileBytes = await System.IO.File.ReadAllBytesAsync(caminhoCompleto);
        var contentType = GetContentType(Path.GetExtension(caminhoCompleto));
        
        return File(fileBytes, contentType, documento.Nome);
    }

    // ─── DELETE /api/user/documentos-gerais/{id} ──────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDocumento(int id)
    {
        var uid = User.GetUserId();
        var documento = await _db.DocumentosGerais!
            .FirstOrDefaultAsync(d => d.Id == id && d.UsuarioId == uid);

        if (documento is null)
            return NotFound(new { message = "Documento não encontrado." });

        // Apagar ficheiro
        if (!string.IsNullOrEmpty(documento.CaminhoFicheiro))
        {
            var caminhoCompleto = Path.Combine(_env.ContentRootPath, documento.CaminhoFicheiro.TrimStart('/'));
            if (System.IO.File.Exists(caminhoCompleto))
                System.IO.File.Delete(caminhoCompleto);
        }

        _db.DocumentosGerais!.Remove(documento);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Documento removido com sucesso." });
    }

    // ─── POST /api/user/documentos-gerais/{id}/favorito ───────────────────────
    [HttpPost("{id:int}/favorito")]
    public async Task<IActionResult> AlternarFavorito(int id)
    {
        var uid = User.GetUserId();
        var documento = await _db.DocumentosGerais!
            .FirstOrDefaultAsync(d => d.Id == id && d.UsuarioId == uid);

        if (documento is null)
            return NotFound(new { message = "Documento não encontrado." });

        documento.Favorito = !documento.Favorito;
        documento.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { favorito = documento.Favorito });
    }

    // ─── GET /api/user/documentos-gerais/estatisticas ─────────────────────────
    [HttpGet("estatisticas")]
    public async Task<IActionResult> GetEstatisticas()
    {
        var uid = User.GetUserId();

        var total = await _db.DocumentosGerais!.CountAsync(d => d.UsuarioId == uid);
        var porTipo = await _db.DocumentosGerais!
            .Where(d => d.UsuarioId == uid)
            .GroupBy(d => d.Tipo)
            .Select(g => new { Tipo = g.Key, Total = g.Count() })
            .ToListAsync();
        var totalDownloads = await _db.DocumentosGerais!.Where(d => d.UsuarioId == uid).SumAsync(d => d.Downloads);
        var totalVisualizacoes = await _db.DocumentosGerais!.Where(d => d.UsuarioId == uid).SumAsync(d => d.Visualizacoes);
        var favoritos = await _db.DocumentosGerais!.CountAsync(d => d.UsuarioId == uid && d.Favorito);

        return Ok(new
        {
            Total = total,
            PorTipo = porTipo,
            TotalDownloads = totalDownloads,
            TotalVisualizacoes = totalVisualizacoes,
            Favoritos = favoritos
        });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroDocumento()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultimo = _db.DocumentosGerais!
            .Where(d => d.NumeroDocumento.StartsWith($"DOC/{ano}/{mes}/"))
            .OrderByDescending(d => d.NumeroDocumento)
            .FirstOrDefault();

        if (ultimo is null)
            return $"DOC/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultimo.NumeroDocumento.Split('/').Last());
        return $"DOC/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private async Task<Dictionary<int, string>> CarregarNomesEntidades(List<DocumentoGeral> documentos)
    {
        var resultado = new Dictionary<int, string>();
        
        var clientesIds = documentos.Where(d => d.EntidadeRelacionada == "Cliente" && d.EntidadeId.HasValue)
            .Select(d => d.EntidadeId!.Value).Distinct().ToList();
        if (clientesIds.Any())
        {
            var clientes = await _db.ClientesCatalogo!
                .Where(c => clientesIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nome);
            foreach (var kv in clientes)
                resultado[kv.Key] = kv.Value;
        }

        var fornecedoresIds = documentos.Where(d => d.EntidadeRelacionada == "Fornecedor" && d.EntidadeId.HasValue)
            .Select(d => d.EntidadeId!.Value).Distinct().ToList();
        if (fornecedoresIds.Any())
        {
            var fornecedores = await _db.FornecedoresCatalogo!
                .Where(f => fornecedoresIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Nome);
            foreach (var kv in fornecedores)
                resultado[kv.Key] = kv.Value;
        }

        var faturasIds = documentos.Where(d => d.EntidadeRelacionada == "Fatura" && d.EntidadeId.HasValue)
            .Select(d => d.EntidadeId!.Value).Distinct().ToList();
        if (faturasIds.Any())
        {
            var faturas = await _db.Faturas!
                .Where(f => faturasIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.NumeroFatura);
            foreach (var kv in faturas)
                resultado[kv.Key] = kv.Value;
        }

        return resultado;
    }

    private static DocumentoGeralResponseDto MapToResponseDto(DocumentoGeral d, Dictionary<int, string> entidadeNomes)
    {
        return new DocumentoGeralResponseDto
        {
            Id = d.Id,
            NumeroDocumento = d.NumeroDocumento,
            Tipo = d.Tipo,
            Nome = d.Nome,
            Descricao = d.Descricao,
            DataDocumento = d.DataDocumento,
            DataCriacao = d.DataCriacao,
            CaminhoFicheiro = d.CaminhoFicheiro,
            TamanhoBytes = d.TamanhoBytes,
            EntidadeRelacionada = d.EntidadeRelacionada,
            EntidadeId = d.EntidadeId,
            EntidadeNome = d.EntidadeId.HasValue && entidadeNomes.TryGetValue(d.EntidadeId.Value, out var nome) ? nome : null,
            Tags = d.Tags,
            Categoria = d.Categoria,
            Favorito = d.Favorito,
            Visualizacoes = d.Visualizacoes,
            Downloads = d.Downloads,
            UltimoAcesso = d.UltimoAcesso,
            Observacoes = d.Observacoes,
            CriadoEm = d.CriadoEm
        };
    }

    private static string GetContentType(string extensao) => extensao.ToLower() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".doc" => "application/msword",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };
}