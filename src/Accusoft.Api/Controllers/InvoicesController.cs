// ══════════════════════════════════════════════════════════════════════════════
// FICHEIRO: src/Accusoft.Api/Controllers/InvoicesController.cs
// ALTERAÇÕES:
//   1. InvoiceDto agora inclui List<FaturaAnexoDto> Anexos
//   2. Todos os .Include(f => f.Itens) passaram a incluir também .Include(f => f.Anexos)
//   3. Adicionados endpoints:
//        POST   /api/user/faturas/{id}/anexos        — upload de anexo
//        GET    /api/user/faturas/{id}/anexos/{aid}  — download de anexo
//        DELETE /api/user/faturas/{id}/anexos/{aid}  — remoção de anexo
// ══════════════════════════════════════════════════════════════════════════════

using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.Controllers;

// ── Request / Response records ────────────────────────────────────────────────

public record CreateInvoiceItemRequest(
    [Required(ErrorMessage = "Marca é obrigatória.")]
    [MaxLength(100)] string Marca,

    [Required(ErrorMessage = "Modelo é obrigatório.")]
    [MaxLength(100)] string Modelo,

    [MaxLength(50)] string Cor,

    [Required(ErrorMessage = "Matrícula é obrigatória.")]
    [MaxLength(20)] string Matricula,

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser superior a zero.")]
    int Quantidade,

    [Range(0, double.MaxValue, ErrorMessage = "Preço unitário não pode ser negativo.")]
    decimal PrecoUnitario
);

public record CreateInvoiceRequest(
    [Required(ErrorMessage = "Nome do cliente é obrigatório.")]
    [MaxLength(200)] string ClienteNome,

    [Required(ErrorMessage = "Contacto do cliente é obrigatório.")]
    [MaxLength(100)] string ClienteContacto,

    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [MaxLength(200)] string? ClienteEmail,

    [MaxLength(300)] string? ClienteMorada,

    [RegularExpression(@"^\d{9}$", ErrorMessage = "NIF deve ter exactamente 9 dígitos.")]
    [MaxLength(20)] string? ClienteNif,

    int? ClienteId,

    [Required(ErrorMessage = "Data do documento é obrigatória.")]
    DateOnly DataDoc,

    [Required(ErrorMessage = "Estado é obrigatório.")]
    [MaxLength(50)] string Estado,

    string? Observacoes,

    [MaxLength(200)] string? QuemExecutou,

    [Range(0, double.MaxValue, ErrorMessage = "Horas de trabalho não podem ser negativas.")]
    decimal? HorasTrabalho,

    string? MaterialUtilizado,

    [Required(ErrorMessage = "É necessário pelo menos um item.")]
    [MinLength(1, ErrorMessage = "É necessário pelo menos um item.")]
    List<CreateInvoiceItemRequest> Itens
);

public record UpdateInvoiceRequest(
    [MaxLength(200)] string? ClienteNome,
    [MaxLength(100)] string? ClienteContacto,
    [EmailAddress][MaxLength(200)] string? ClienteEmail,
    [MaxLength(300)] string? ClienteMorada,
    [RegularExpression(@"^\d{9}$", ErrorMessage = "NIF deve ter exactamente 9 dígitos.")][MaxLength(20)] string? ClienteNif,
    int? ClienteId,
    DateOnly? DataDoc,
    [MaxLength(50)] string? Estado,
    string? Observacoes,
    [MaxLength(200)] string? QuemExecutou,
    [Range(0, double.MaxValue)] decimal? HorasTrabalho,
    string? MaterialUtilizado,
    List<CreateInvoiceItemRequest>? Itens
);

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record FaturaAnexoDto(
    int Id,
    string NomeOriginal,
    string MimeType,
    long TamanhoBytes,
    string TamanhoFormatado,
    DateTimeOffset CriadoEm
);

public record InvoiceDto(
    int Id,
    string NumeroFatura,
    int? ClienteId,
    string ClienteNome,
    string ClienteContacto,
    string? ClienteEmail,
    string? ClienteMorada,
    string? ClienteNif,
    string DataDoc,
    string Estado,
    decimal ValorTotal,
    string? Observacoes,
    string? QuemExecutou,
    decimal? HorasTrabalho,
    string? MaterialUtilizado,
    DateTimeOffset CriadoEm,
    List<InvoiceItemDto> Itens,
    List<FaturaAnexoDto> Anexos    // NOVO
);

public record InvoiceItemDto(
    int Id,
    string Marca,
    string Modelo,
    string Cor,
    string Matricula,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal
);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/user/faturas")]
[Authorize]
public class InvoicesController(AppDbContext db, IFileStorageService fileStorage) : ControllerBase
{
    private static readonly SemaphoreSlim _invoiceLock = new(1, 1);

    // Allowed MIME types for attachments
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/octet-stream",
        "application/x-pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/x-png"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private const long MaxAttachmentBytes = 5 * 1024 * 1024; // 5 MB

    // ── GET /api/user/faturas ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetFaturas(
        [FromQuery] string? estado,
        [FromQuery] string? search,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();
        var query = db.Faturas
            .AsNoTracking()
            .Include(f => f.Itens)
            .Include(f => f.Anexos)   // NOVO
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(f => f.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(f => f.Estado.ToLower() == estado.ToLower());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(f =>
                f.ClienteNome.ToLower().Contains(s) ||
                f.NumeroFatura.ToLower().Contains(s));
        }

        if (dataInicio.HasValue)
            query = query.Where(f => f.DataDoc >= DateOnly.FromDateTime(dataInicio.Value));

        if (dataFim.HasValue)
            query = query.Where(f => f.DataDoc <= DateOnly.FromDateTime(dataFim.Value));

        var total = await query.CountAsync();
        var faturas = await query
            .OrderByDescending(f => f.DataDoc)
            .ThenByDescending(f => f.CriadoEm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = faturas.Select(MapToDto) });
    }

    // ── GET /api/user/faturas/{id} ────────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFatura(int id)
    {
        var uid = User.GetUserId();
        var query = db.Faturas
            .AsNoTracking()
            .Include(f => f.Itens)
            .Include(f => f.Anexos)   // NOVO
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(f => f.UsuarioId == uid);

        var fatura = await query.FirstOrDefaultAsync(f => f.Id == id);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        return Ok(MapToDto(fatura));
    }

    // ── POST /api/user/faturas ────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateFatura([FromBody] CreateInvoiceRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (req.Itens is null || req.Itens.Count == 0)
            return BadRequest(new { message = "É necessário pelo menos um item na fatura." });

        var itensInvalidos = req.Itens.Where(i => i.Quantidade <= 0 || i.PrecoUnitario < 0).ToList();
        if (itensInvalidos.Count > 0)
            return BadRequest(new { message = "Todos os itens devem ter quantidade > 0 e preço ≥ 0." });

        var uid = User.GetUserId();

        if (req.ClienteId.HasValue)
        {
            var clienteQuery = db.ClientesCatalogo.AsQueryable();
            if (!User.IsAdmin())
                clienteQuery = clienteQuery.Where(c => c.CriadoPor == uid);

            var clienteExiste = await clienteQuery.AnyAsync(c => c.Id == req.ClienteId.Value);
            if (!clienteExiste)
                return BadRequest(new { message = "Cliente seleccionado não existe ou não pertence a este utilizador." });
        }

        var valorTotalServidor = req.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);

        string numeroFatura;
        await _invoiceLock.WaitAsync();
        try { numeroFatura = await GerarNumeroFaturaAsync(); }
        finally { _invoiceLock.Release(); }

        var fatura = new Invoice
        {
            NumeroFatura    = numeroFatura,
            ClienteId       = req.ClienteId,
            ClienteNome     = req.ClienteNome.Trim(),
            ClienteContacto = req.ClienteContacto.Trim(),
            ClienteEmail    = req.ClienteEmail?.Trim(),
            ClienteMorada   = req.ClienteMorada?.Trim(),
            ClienteNif      = req.ClienteNif?.Trim(),
            DataDoc         = req.DataDoc,
            Estado          = req.Estado,
            Observacoes     = req.Observacoes?.Trim(),
            QuemExecutou    = req.QuemExecutou?.Trim(),
            HorasTrabalho   = req.HorasTrabalho,
            MaterialUtilizado = req.MaterialUtilizado?.Trim(),
            UsuarioId       = uid,
            CriadoEm        = DateTimeOffset.UtcNow,
            AtualizadoEm    = DateTimeOffset.UtcNow,
            ValorTotal      = valorTotalServidor
        };

        db.Faturas.Add(fatura);
        await db.SaveChangesAsync();

        var faturaItens = req.Itens.Select(item => new InvoiceItem
        {
            FaturaId      = fatura.Id,
            Marca         = item.Marca.Trim(),
            Modelo        = item.Modelo.Trim(),
            Cor           = item.Cor?.Trim() ?? string.Empty,
            Matricula     = item.Matricula.Trim().ToUpperInvariant(),
            Quantidade    = item.Quantidade,
            PrecoUnitario = item.PrecoUnitario,
            Subtotal      = item.Quantidade * item.PrecoUnitario
        }).ToList();

        db.FaturaItens.AddRange(faturaItens);
        await db.SaveChangesAsync();

        fatura.Itens = faturaItens;
        return Created($"/api/user/faturas/{fatura.Id}", MapToDto(fatura));
    }

    // ── PUT /api/user/faturas/{id} ────────────────────────────────────────────

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFatura(int id, [FromBody] UpdateInvoiceRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var query = db.Faturas
            .Include(f => f.Itens)
            .Include(f => f.Anexos)   // NOVO
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(f => f.UsuarioId == uid);

        var fatura = await query.FirstOrDefaultAsync(f => f.Id == id);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        if (req.ClienteId.HasValue)
        {
            var clienteQuery = db.ClientesCatalogo.AsQueryable();
            if (!User.IsAdmin())
                clienteQuery = clienteQuery.Where(c => c.CriadoPor == uid);

            var clienteExiste = await clienteQuery.AnyAsync(c => c.Id == req.ClienteId.Value);
            if (!clienteExiste)
                return BadRequest(new { message = "Cliente seleccionado não existe ou não pertence a este utilizador." });
            fatura.ClienteId = req.ClienteId.Value;
        }

        if (!string.IsNullOrWhiteSpace(req.ClienteNome))     fatura.ClienteNome     = req.ClienteNome.Trim();
        if (!string.IsNullOrWhiteSpace(req.ClienteContacto)) fatura.ClienteContacto = req.ClienteContacto.Trim();
        if (req.ClienteEmail      is not null) fatura.ClienteEmail      = req.ClienteEmail.Trim();
        if (req.ClienteMorada     is not null) fatura.ClienteMorada     = req.ClienteMorada.Trim();
        if (req.ClienteNif        is not null) fatura.ClienteNif        = req.ClienteNif.Trim();
        if (req.DataDoc.HasValue)              fatura.DataDoc           = req.DataDoc.Value;
        if (!string.IsNullOrWhiteSpace(req.Estado)) fatura.Estado      = req.Estado;
        if (req.Observacoes       is not null) fatura.Observacoes       = req.Observacoes.Trim();
        if (req.QuemExecutou      is not null) fatura.QuemExecutou      = req.QuemExecutou.Trim();
        if (req.HorasTrabalho.HasValue)        fatura.HorasTrabalho     = req.HorasTrabalho.Value;
        if (req.MaterialUtilizado is not null) fatura.MaterialUtilizado = req.MaterialUtilizado.Trim();

        if (req.Itens is { Count: > 0 })
        {
            var itensInvalidos = req.Itens.Where(i => i.Quantidade <= 0 || i.PrecoUnitario < 0).ToList();
            if (itensInvalidos.Count > 0)
                return BadRequest(new { message = "Todos os itens devem ter quantidade > 0 e preço ≥ 0." });

            db.FaturaItens.RemoveRange(fatura.Itens);

            var novosItens = req.Itens.Select(item => new InvoiceItem
            {
                FaturaId      = fatura.Id,
                Marca         = item.Marca.Trim(),
                Modelo        = item.Modelo.Trim(),
                Cor           = item.Cor?.Trim() ?? string.Empty,
                Matricula     = item.Matricula.Trim().ToUpperInvariant(),
                Quantidade    = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Subtotal      = item.Quantidade * item.PrecoUnitario
            }).ToList();

            db.FaturaItens.AddRange(novosItens);
            fatura.ValorTotal = novosItens.Sum(i => i.Subtotal);
            fatura.Itens      = novosItens;
        }

        fatura.AtualizadoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToDto(fatura));
    }

    // ── DELETE /api/user/faturas/{id} ─────────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFatura(int id)
    {
        var uid = User.GetUserId();
        var query = db.Faturas
            .Include(f => f.Itens)
            .Include(f => f.Anexos)   // NOVO — para apagar ficheiros físicos
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(f => f.UsuarioId == uid);

        var fatura = await query.FirstOrDefaultAsync(f => f.Id == id);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        // Remover ficheiros físicos antes de eliminar os registos
        foreach (var anexo in fatura.Anexos)
        {
            try { fileStorage.Delete(anexo.PathUrl); }
            catch { /* ignora erros de limpeza de ficheiros */ }
        }

        db.FaturaItens.RemoveRange(fatura.Itens);
        db.FaturaAnexos.RemoveRange(fatura.Anexos);
        db.Faturas.Remove(fatura);
        await db.SaveChangesAsync();

        return Ok(new { message = "Fatura removida com sucesso." });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NOVOS ENDPOINTS — Anexos
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/user/faturas/{id}/anexos
    /// Faz upload de um ficheiro e associa-o à fatura.
    /// </summary>
    [HttpPost("{id:int}/anexos")]
    [RequestSizeLimit(5 * 1024 * 1024 + 1024)] // 5 MB + overhead
    public async Task<IActionResult> UploadAnexo(int id, [FromForm] IFormFile ficheiro)
    {
        if (ficheiro is null || ficheiro.Length == 0)
            return BadRequest(new { message = "Ficheiro é obrigatório." });

        if (ficheiro.Length > MaxAttachmentBytes)
            return BadRequest(new { message = "O ficheiro não pode exceder 5 MB." });

        var ext = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Extensão de ficheiro não permitida." });

        if (!AllowedMimeTypes.Contains(ficheiro.ContentType))
        {
            var mime = ficheiro.ContentType?.ToLowerInvariant() ?? string.Empty;
            var isPdfAlias = ext == ".pdf" && (mime == "application/octet-stream" || mime == "application/x-pdf");
            var isImageAlias = (ext == ".jpg" || ext == ".jpeg" || ext == ".png") && mime.StartsWith("image/");

            if (!isPdfAlias && !isImageAlias)
                return BadRequest(new { message = "Apenas ficheiros PDF, JPG ou PNG são permitidos." });
        }

        var uid = User.GetUserId();

        // Verificar que a fatura existe e pertence ao utilizador
        var faturaQuery = db.Faturas.AsQueryable();
        if (!User.IsAdmin())
            faturaQuery = faturaQuery.Where(f => f.UsuarioId == uid);

        var faturaExiste = await faturaQuery.AnyAsync(f => f.Id == id);
        if (!faturaExiste)
            return NotFound(new { message = "Fatura não encontrada." });

        // Guardar o ficheiro via IFileStorageService (reutiliza a infra existente)
        var (pathUrl, _, _) = await fileStorage.SaveAsync(ficheiro, uid);

        var anexo = new FaturaAnexo
        {
            FaturaId      = id,
            NomeOriginal  = Path.GetFileName(ficheiro.FileName),
            NomeFicheiro  = Path.GetFileName(pathUrl),
            PathUrl       = pathUrl,
            MimeType      = ficheiro.ContentType ?? string.Empty,
            TamanhoBytes  = ficheiro.Length,
            UsuarioId     = uid,
            CriadoEm      = DateTimeOffset.UtcNow
        };

        db.FaturaAnexos.Add(anexo);
        await db.SaveChangesAsync();

        return Ok(MapAnexoDto(anexo));
    }

    /// <summary>
    /// GET /api/user/faturas/{id}/anexos/{anexoId}
    /// Faz download de um anexo da fatura.
    /// </summary>
    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> DownloadAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        var faturaQuery = db.Faturas.AsQueryable();
        if (!User.IsAdmin())
            faturaQuery = faturaQuery.Where(f => f.UsuarioId == uid);

        var faturaExiste = await faturaQuery.AnyAsync(f => f.Id == id);
        if (!faturaExiste)
            return NotFound(new { message = "Fatura não encontrada." });

        var anexo = await db.FaturaAnexos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.FaturaId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        // Obter o stream do ficheiro via IFileStorageService
        var stream = fileStorage.GetStream(anexo.PathUrl);
        if (stream is null)
            return NotFound(new { message = "Ficheiro não encontrado no servidor." });

        return File(stream, anexo.MimeType, anexo.NomeOriginal);
    }

    /// <summary>
    /// DELETE /api/user/faturas/{id}/anexos/{anexoId}
    /// Remove um anexo da fatura.
    /// </summary>
    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> RemoverAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        var faturaQuery = db.Faturas.AsQueryable();
        if (!User.IsAdmin())
            faturaQuery = faturaQuery.Where(f => f.UsuarioId == uid);

        var faturaExiste = await faturaQuery.AnyAsync(f => f.Id == id);
        if (!faturaExiste)
            return NotFound(new { message = "Fatura não encontrada." });

        var anexo = await db.FaturaAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.FaturaId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        try { fileStorage.Delete(anexo.PathUrl); }
        catch { /* ignora falhas de limpeza de ficheiros */ }

        db.FaturaAnexos.Remove(anexo);
        await db.SaveChangesAsync();

        return Ok(new { message = "Anexo removido com sucesso." });
    }

    // ── GET /api/user/faturas/{id}/pdf ────────────────────────────────────────

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GerarPdf(int id)
    {
        var uid = User.GetUserId();
        var query = db.Faturas
            .Include(f => f.Itens)
            .Include(f => f.Anexos)
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(f => f.UsuarioId == uid);

        var fatura = await query.FirstOrDefaultAsync(f => f.Id == id);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        var pdfBytes = GerarPdfFatura(fatura);
        return File(pdfBytes, "application/pdf", $"Fatura_{fatura.NumeroFatura}.pdf");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GerarNumeroFaturaAsync()
    {
        var ano = DateTime.UtcNow.Year;
        var prefixo = $"FT/{ano}/";

        var ultimoNumero = await db.Faturas
            .Where(f => f.NumeroFatura.StartsWith(prefixo))
            .OrderByDescending(f => f.NumeroFatura)
            .Select(f => f.NumeroFatura)
            .FirstOrDefaultAsync();

        if (ultimoNumero is null)
            return $"{prefixo}0001";

        var partes = ultimoNumero.Split('/');
        if (partes.Length >= 3 && int.TryParse(partes[^1], out var n))
            return $"{prefixo}{(n + 1):D4}";

        return $"{prefixo}0001";
    }

    private static string FormatarTamanho(long bytes)
    {
        if (bytes < 1024)        return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private static FaturaAnexoDto MapAnexoDto(FaturaAnexo a) => new(
        a.Id,
        a.NomeOriginal,
        a.MimeType,
        a.TamanhoBytes,
        FormatarTamanho(a.TamanhoBytes),
        a.CriadoEm
    );

    private static InvoiceDto MapToDto(Invoice f) => new(
        f.Id,
        f.NumeroFatura,
        f.ClienteId,
        f.ClienteNome,
        f.ClienteContacto,
        f.ClienteEmail,
        f.ClienteMorada,
        f.ClienteNif,
        f.DataDoc.ToString("yyyy-MM-dd"),
        f.Estado,
        f.ValorTotal,
        f.Observacoes,
        f.QuemExecutou,
        f.HorasTrabalho,
        f.MaterialUtilizado,
        f.CriadoEm,
        f.Itens.Select(i => new InvoiceItemDto(
            i.Id, i.Marca, i.Modelo, i.Cor, i.Matricula,
            i.Quantidade, i.PrecoUnitario, i.Subtotal
        )).ToList(),
        f.Anexos.Select(MapAnexoDto).ToList()   // NOVO
    );

    private static byte[] GerarPdfFatura(Invoice fatura)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ACCUSOFT").FontSize(20).Bold();
                        col.Item().Text("Sistema de Gestão").FontSize(10).FontColor(Colors.Grey.Medium);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text($"Fatura Nº: {fatura.NumeroFatura}").Bold();
                        col.Item().AlignRight().Text($"Data: {fatura.DataDoc:dd/MM/yyyy}");
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(20).Column(c =>
                    {
                        c.Item().Text("INFORMAÇÕES DO CLIENTE").FontSize(12).Bold();
                        c.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text($"Nome: {fatura.ClienteNome}");
                            row.RelativeItem().Text($"Contacto: {fatura.ClienteContacto}");
                        });
                        if (!string.IsNullOrEmpty(fatura.ClienteNif))
                            c.Item().Text($"NIF: {fatura.ClienteNif}");
                        if (!string.IsNullOrEmpty(fatura.ClienteMorada))
                            c.Item().Text($"Morada: {fatura.ClienteMorada}");
                        if (!string.IsNullOrEmpty(fatura.ClienteEmail))
                            c.Item().Text($"Email: {fatura.ClienteEmail}");
                    });

                    if (!string.IsNullOrEmpty(fatura.QuemExecutou) || fatura.HorasTrabalho.HasValue)
                    {
                        col.Item().PaddingTop(14).Column(c =>
                        {
                            c.Item().Text("SERVIÇO EXECUTADO").FontSize(12).Bold();
                            if (!string.IsNullOrEmpty(fatura.QuemExecutou))
                                c.Item().PaddingTop(4).Text($"Técnico: {fatura.QuemExecutou}");
                            if (fatura.HorasTrabalho.HasValue)
                                c.Item().Text($"Horas de trabalho: {fatura.HorasTrabalho:N2}h");
                            if (!string.IsNullOrEmpty(fatura.MaterialUtilizado))
                                c.Item().Text($"Material: {fatura.MaterialUtilizado}");
                        });
                    }

                    col.Item().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Marca").Bold();
                            header.Cell().Text("Modelo").Bold();
                            header.Cell().Text("Cor").Bold();
                            header.Cell().Text("Matrícula").Bold();
                            header.Cell().Text("Qtd").Bold();
                            header.Cell().AlignRight().Text("P. Unit.").Bold();
                            header.Cell().AlignRight().Text("Subtotal").Bold();
                        });

                        foreach (var item in fatura.Itens)
                        {
                            table.Cell().Text(item.Marca);
                            table.Cell().Text(item.Modelo);
                            table.Cell().Text(item.Cor);
                            table.Cell().Text(item.Matricula);
                            table.Cell().Text(item.Quantidade.ToString());
                            table.Cell().AlignRight().Text($"{item.PrecoUnitario:C}");
                            table.Cell().AlignRight().Text($"{item.Subtotal:C}");
                        }
                    });

                    col.Item().PaddingTop(16).AlignRight()
                        .Text($"TOTAL: {fatura.ValorTotal:C}").FontSize(14).Bold();

                    col.Item().PaddingTop(8).Row(row =>
                        row.RelativeItem().Text($"Estado: {fatura.Estado}"));

                    if (!string.IsNullOrEmpty(fatura.Observacoes))
                        col.Item().PaddingTop(12).Column(c =>
                        {
                            c.Item().Text("Observações:").Bold();
                            c.Item().Text(fatura.Observacoes);
                        });

                    // Listagem de anexos no PDF (informativa)
                    if (fatura.Anexos.Count > 0)
                    {
                        col.Item().PaddingTop(16).Column(c =>
                        {
                            c.Item().Text("DOCUMENTOS ANEXADOS").FontSize(11).Bold();
                            foreach (var anexo in fatura.Anexos)
                                c.Item().PaddingTop(2).Text($"• {anexo.NomeOriginal} ({FormatarTamanho(anexo.TamanhoBytes)})");
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Documento gerado eletronicamente em ");
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                });
            });
        });

        return document.GeneratePdf();
    }
}