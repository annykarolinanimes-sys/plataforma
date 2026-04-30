using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Accusoft.Api.Controllers;

public record CreateInvoiceRequest(
    string ClienteNome,
    string ClienteContacto,
    string? ClienteEmail,
    string? ClienteMorada,
    string? ClienteNif,
    DateOnly DataDoc,
    string Estado,
    string? Observacoes,
    string? QuemExecutou,      
    decimal? HorasTrabalho,     
    string? MaterialUtilizado,
    List<CreateInvoiceItemRequest> Itens
);

public record CreateInvoiceItemRequest(
    string Marca,
    string Modelo,
    string Cor,
    string Matricula,
    int Quantidade,
    decimal PrecoUnitario
);

public record UpdateInvoiceRequest(
    string? ClienteNome,
    string? ClienteContacto,
    string? ClienteEmail,
    string? ClienteMorada,
    string? ClienteNif,
    DateOnly? DataDoc,
    string? Estado,
    string? Observacoes,
    string? QuemExecutou,      
    decimal? HorasTrabalho,     
    string? MaterialUtilizado,
    List<CreateInvoiceItemRequest>? Itens
);

public record InvoiceDto(
    int Id,
    string NumeroFatura,
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
    List<InvoiceItemDto> Itens
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

[ApiController]
[Route("api/user/faturas")]
[Authorize]
public class InvoicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFaturas(
        [FromQuery] string? estado,
        [FromQuery] string? search,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var uid = User.GetUserId();
        var query = db.Faturas
            .AsNoTracking()
            .Include(f => f.Itens)
            .Where(f => f.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(f => f.Estado.ToLower() == estado.ToLower());

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f =>
                f.ClienteNome.ToLower().Contains(search.ToLower()) ||
                f.NumeroFatura.ToLower().Contains(search.ToLower()));

        if (dataInicio.HasValue)
            query = query.Where(f => f.DataDoc >= DateOnly.FromDateTime(dataInicio.Value));

        if (dataFim.HasValue)
            query = query.Where(f => f.DataDoc <= DateOnly.FromDateTime(dataFim.Value));

        var faturas = await query
            .OrderByDescending(f => f.DataDoc)
            .ThenByDescending(f => f.CriadoEm)
            .ToListAsync();

        return Ok(faturas.Select(MapToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFatura(int id)
    {
        var uid = User.GetUserId();
        var fatura = await db.Faturas
            .AsNoTracking()
            .Include(f => f.Itens)
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        return Ok(MapToDto(fatura));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFatura([FromBody] CreateInvoiceRequest req)
    {
        var uid = User.GetUserId();

        var numeroFatura = GerarNumeroFatura();

        var fatura = new Invoice
        {
            NumeroFatura = numeroFatura,
            ClienteNome = req.ClienteNome,
            ClienteContacto = req.ClienteContacto,
            ClienteEmail = req.ClienteEmail,
            ClienteMorada = req.ClienteMorada,
            ClienteNif = req.ClienteNif,
            DataDoc = req.DataDoc,
            Estado = req.Estado,
            Observacoes = req.Observacoes,
            QuemExecutou = req.QuemExecutou,           
            HorasTrabalho = req.HorasTrabalho,         
            MaterialUtilizado = req.MaterialUtilizado, 
            UsuarioId = uid,  
            CriadoEm = DateTimeOffset.UtcNow,
            AtualizadoEm = DateTimeOffset.UtcNow,
            ValorTotal = req.Itens.Sum(i => i.Quantidade * i.PrecoUnitario)
        };

        db.Faturas.Add(fatura);
        await db.SaveChangesAsync();

        foreach (var item in req.Itens)
        {
            var faturaItem = new InvoiceItem
            {
                FaturaId = fatura.Id,
                Marca = item.Marca,
                Modelo = item.Modelo,
                Cor = item.Cor,
                Matricula = item.Matricula,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Subtotal = item.Quantidade * item.PrecoUnitario
            };
            db.FaturaItens.Add(faturaItem);
        }

        await db.SaveChangesAsync();

        return Created($"/api/user/faturas/{fatura.Id}", MapToDto(fatura));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFatura(int id, [FromBody] UpdateInvoiceRequest req)
    {
        var uid = User.GetUserId();
        var fatura = await db.Faturas
            .Include(f => f.Itens)
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        if (!string.IsNullOrWhiteSpace(req.ClienteNome))
            fatura.ClienteNome = req.ClienteNome;

        if (!string.IsNullOrWhiteSpace(req.ClienteContacto))
            fatura.ClienteContacto = req.ClienteContacto;

        if (req.ClienteEmail is not null)
            fatura.ClienteEmail = req.ClienteEmail;

        if (req.ClienteMorada is not null)
            fatura.ClienteMorada = req.ClienteMorada;

        if (req.ClienteNif is not null)
            fatura.ClienteNif = req.ClienteNif;

        if (req.DataDoc.HasValue)
            fatura.DataDoc = req.DataDoc.Value;

        if (!string.IsNullOrWhiteSpace(req.Estado))
            fatura.Estado = req.Estado;

        if (req.Observacoes is not null)
            fatura.Observacoes = req.Observacoes;

        if (req.QuemExecutou is not null)
            fatura.QuemExecutou = req.QuemExecutou;

        if (req.HorasTrabalho.HasValue)
            fatura.HorasTrabalho = req.HorasTrabalho.Value;

        if (req.MaterialUtilizado is not null)
            fatura.MaterialUtilizado = req.MaterialUtilizado;

        if (req.Itens is not null && req.Itens.Any())
        {
            db.FaturaItens.RemoveRange(fatura.Itens);

            foreach (var item in req.Itens)
            {
                db.FaturaItens.Add(new InvoiceItem
                {
                    FaturaId = fatura.Id,
                    Marca = item.Marca,
                    Modelo = item.Modelo,
                    Cor = item.Cor,
                    Matricula = item.Matricula,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = item.PrecoUnitario,
                    Subtotal = item.Quantidade * item.PrecoUnitario
                });
            }

            fatura.ValorTotal = req.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        }

        fatura.AtualizadoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToDto(fatura));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFatura(int id)
    {
        var uid = User.GetUserId();
        var fatura = await db.Faturas
            .Include(f => f.Itens)
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        db.FaturaItens.RemoveRange(fatura.Itens);
        db.Faturas.Remove(fatura);
        await db.SaveChangesAsync();

        return Ok(new { message = "Fatura removida com sucesso." });
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GerarPdf(int id)
    {
        var uid = User.GetUserId();
        var fatura = await db.Faturas
            .Include(f => f.Itens)
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fatura is null)
            return NotFound(new { message = "Fatura não encontrada." });

        var pdfBytes = GerarPdfFatura(fatura);
        
        return File(pdfBytes, "application/pdf", $"Fatura_{fatura.NumeroFatura}.pdf");
    }

    private byte[] GerarPdfFatura(Invoice fatura)
{
    QuestPDF.Settings.License = LicenseType.Community;

    var document = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11));

            page.Header()
                .Row(row =>
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
                col.Item().PaddingTop(20).Column(clienteCol =>
                {
                    clienteCol.Item().Text("INFORMAÇÕES DO CLIENTE").FontSize(12).Bold();
                    clienteCol.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"Nome: {fatura.ClienteNome}");
                        row.RelativeItem().Text($"Contacto: {fatura.ClienteContacto}");
                    });
                    if (!string.IsNullOrEmpty(fatura.ClienteNif))
                        clienteCol.Item().Text($"NIF: {fatura.ClienteNif}");
                    if (!string.IsNullOrEmpty(fatura.ClienteMorada))
                        clienteCol.Item().Text($"Morada: {fatura.ClienteMorada}");
                    if (!string.IsNullOrEmpty(fatura.ClienteEmail))
                        clienteCol.Item().Text($"Email: {fatura.ClienteEmail}");
                });

                col.Item().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Marca").Bold();
                        header.Cell().Text("Modelo").Bold();
                        header.Cell().Text("Cor").Bold();
                        header.Cell().Text("Matrícula").Bold();
                        header.Cell().Text("Qtd").Bold();
                        header.Cell().Text("Subtotal").Bold();
                    });

                    foreach (var item in fatura.Itens)
                    {
                        table.Cell().Text(item.Marca);
                        table.Cell().Text(item.Modelo);
                        table.Cell().Text(item.Cor);
                        table.Cell().Text(item.Matricula);
                        table.Cell().Text(item.Quantidade.ToString());
                        table.Cell().Text($"{item.Subtotal:C}");
                    }
                });

                col.Item().PaddingTop(20).AlignRight().Text($"TOTAL: {fatura.ValorTotal:C}").FontSize(14).Bold();
                
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text($"Estado: {fatura.Estado}");
                });
            });

            page.Footer()
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("Documento gerado eletronicamente em ");
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                });
        });
    });

    return document.GeneratePdf();
}

    private string GerarNumeroFatura()
    {
        var ano = DateTime.Now.Year;
        var ultimaFatura = db.Faturas
            .Where(f => f.NumeroFatura.StartsWith($"FT/{ano}/"))
            .OrderByDescending(f => f.NumeroFatura)
            .FirstOrDefault();

        if (ultimaFatura is null)
            return $"FT/{ano}/0001";

        var ultimoNumero = int.Parse(ultimaFatura.NumeroFatura.Split('/').Last());
        return $"FT/{ano}/{ultimoNumero + 1:D4}";
    }

    private static InvoiceDto MapToDto(Invoice f) => new(
        f.Id,
        f.NumeroFatura,
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
            i.Id,
            i.Marca,
            i.Modelo,
            i.Cor,
            i.Matricula,
            i.Quantidade,
            i.PrecoUnitario,
            i.Subtotal
        )).ToList()
    );
}