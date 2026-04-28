using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/guias")]
[Authorize]
public class GuiasController : ControllerBase
{
    private readonly AppDbContext _db;

    public GuiasController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/guias ──────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetGuias(
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.Guias!
            .AsNoTracking()
            .Include(g => g.Cliente)
            .Include(g => g.Transportadora)
            .Include(g => g.Atribuicao)
            .Where(g => g.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(g => g.Tipo == tipo);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(g => g.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(g =>
                g.NumeroGuia.ToLower().Contains(s) ||
                (g.Cliente != null && g.Cliente.Nome.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(g => g.DataEmissao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<GuiaResponseDto>
        {
            Items = items.Select(MapToResponseDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/guias/{id} ─────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGuia(int id)
    {
        var uid = User.GetUserId();
        var guia = await _db.Guias!
            .AsNoTracking()
            .Include(g => g.Cliente)
            .Include(g => g.Transportadora)
            .Include(g => g.Atribuicao)
            .Include(g => g.Itens)
                .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(g => g.Id == id && g.UsuarioId == uid);

        if (guia is null)
            return NotFound(new { message = "Guia não encontrada." });

        return Ok(MapToResponseDto(guia));
    }

    // ─── POST /api/user/guias ─────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateGuia([FromBody] GuiaCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        // Buscar dados do cliente e transportadora para calcular totais
        var cliente = dto.ClienteId.HasValue 
            ? await _db.ClientesCatalogo!.FindAsync(dto.ClienteId.Value) 
            : null;
        
        var transportadora = dto.TransportadoraId.HasValue 
            ? await _db.TransportadorasCatalogo!.FindAsync(dto.TransportadoraId.Value) 
            : null;
        
        var atribuicao = dto.AtribuicaoId.HasValue 
            ? await _db.Atribuicoes!.FindAsync(dto.AtribuicaoId.Value) 
            : null;

        // Calcular totais dos itens
        decimal pesoTotal = 0;
        int volumeTotal = 0;
        int totalVolumes = 0;

        var itens = new List<GuiaItem>();
        foreach (var itemDto in dto.Itens)
        {
            var produto = await _db.Produtos!.FindAsync(itemDto.ProdutoId);
            if (produto is null)
                return BadRequest(new { message = $"Produto ID {itemDto.ProdutoId} não encontrado." });

            var pesoItem = produto.PesoUnitario * itemDto.Quantidade;
            var volumeItem = produto.VolumeUnitario * itemDto.Quantidade;

            pesoTotal += (decimal)pesoItem;
            volumeTotal += volumeItem;
            totalVolumes += itemDto.Quantidade;

            itens.Add(new GuiaItem
            {
                ProdutoId = itemDto.ProdutoId,
                Quantidade = itemDto.Quantidade,
                PesoUnitario = produto.PesoUnitario,
                PesoTotal = (decimal)pesoItem,
                VolumeUnitario = produto.VolumeUnitario,
                VolumeTotal = volumeItem,
                Lote = itemDto.Lote,
                Observacoes = itemDto.Observacoes
            });
        }

        var guia = new Guia
        {
            NumeroGuia = GerarNumeroGuia(),
            Tipo = dto.Tipo,
            Status = "Pendente",
            DataEmissao = DateTime.UtcNow,
            AtribuicaoId = dto.AtribuicaoId,
            ClienteId = dto.ClienteId,
            TransportadoraId = dto.TransportadoraId,
            EnderecoOrigem = dto.EnderecoOrigem ?? atribuicao?.EnderecoOrigem,
            EnderecoDestino = dto.EnderecoDestino ?? atribuicao?.EnderecoDestino,
            TotalItens = itens.Count,
            PesoTotalKg = pesoTotal,
            VolumeTotalM3 = volumeTotal,
            TotalVolumes = totalVolumes,
            DataPrevistaEntrega = dto.DataPrevistaEntrega,
            Observacoes = dto.Observacoes,
            InstrucoesEspeciais = dto.InstrucoesEspeciais,
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now,
            Itens = itens
        };

        _db.Guias!.Add(guia);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetGuia), new { id = guia.Id }, MapToResponseDto(guia));
    }

    // ─── PUT /api/user/guias/{id} ─────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateGuia(int id, [FromBody] GuiaUpdateDto dto)
    {
        var uid = User.GetUserId();
        var guia = await _db.Guias!
            .Include(g => g.Itens)
            .FirstOrDefaultAsync(g => g.Id == id && g.UsuarioId == uid);

        if (guia is null)
            return NotFound(new { message = "Guia não encontrada." });

        if (!string.IsNullOrWhiteSpace(dto.Status))
            guia.Status = dto.Status;

        if (dto.DataPrevistaEntrega.HasValue)
            guia.DataPrevistaEntrega = dto.DataPrevistaEntrega.Value;

        if (dto.DataEntregaReal.HasValue)
            guia.DataEntregaReal = dto.DataEntregaReal.Value;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            guia.Observacoes = dto.Observacoes;

        if (!string.IsNullOrWhiteSpace(dto.InstrucoesEspeciais))
            guia.InstrucoesEspeciais = dto.InstrucoesEspeciais;

        // Atualizar itens
        if (dto.Itens != null && dto.Itens.Any())
        {
            var currentItemIds = guia.Itens.Select(i => i.Id).ToList();
            var updatedItemIds = dto.Itens.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToList();
            var toRemove = currentItemIds.Except(updatedItemIds).ToList();

            foreach (var itemId in toRemove)
            {
                var item = guia.Itens.FirstOrDefault(i => i.Id == itemId);
                if (item != null)
                    _db.GuiaItens!.Remove(item);
            }

            foreach (var itemDto in dto.Itens)
            {
                if (itemDto.Id.HasValue)
                {
                    var existingItem = guia.Itens.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                    if (existingItem != null)
                    {
                        if (itemDto.Quantidade.HasValue)
                            existingItem.Quantidade = itemDto.Quantidade.Value;
                        if (!string.IsNullOrWhiteSpace(itemDto.Lote))
                            existingItem.Lote = itemDto.Lote;
                        if (!string.IsNullOrWhiteSpace(itemDto.Observacoes))
                            existingItem.Observacoes = itemDto.Observacoes;

                        existingItem.PesoTotal = existingItem.PesoUnitario * existingItem.Quantidade;
                        existingItem.VolumeTotal = existingItem.VolumeUnitario * existingItem.Quantidade;
                    }
                }
                else if (itemDto.ProdutoId.HasValue && itemDto.Quantidade.HasValue)
                {
                    var produto = await _db.Produtos!.FindAsync(itemDto.ProdutoId.Value);
                    if (produto != null)
                    {
                        _db.GuiaItens!.Add(new GuiaItem
                        {
                            GuiaId = guia.Id,
                            ProdutoId = itemDto.ProdutoId.Value,
                            Quantidade = itemDto.Quantidade.Value,
                            PesoUnitario = produto.PesoUnitario,
                            PesoTotal = produto.PesoUnitario * itemDto.Quantidade.Value,
                            VolumeUnitario = produto.VolumeUnitario,
                            VolumeTotal = produto.VolumeUnitario * itemDto.Quantidade.Value,
                            Lote = itemDto.Lote,
                            Observacoes = itemDto.Observacoes
                        });
                    }
                }
            }

            // Recalcular totais
            guia.TotalItens = guia.Itens.Count;
            guia.PesoTotalKg = guia.Itens.Sum(i => i.PesoTotal);
            guia.VolumeTotalM3 = guia.Itens.Sum(i => i.VolumeTotal);
            guia.TotalVolumes = guia.Itens.Sum(i => i.Quantidade);
        }

        guia.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(guia));
    }

    // ─── POST /api/user/guias/{id}/imprimir ───────────────────────────────────
    [HttpPost("{id:int}/imprimir")]
    public async Task<IActionResult> ImprimirGuia(int id)
    {
        var uid = User.GetUserId();
        var guia = await _db.Guias!
            .AsNoTracking()
            .Include(g => g.Cliente)
            .Include(g => g.Transportadora)
            .Include(g => g.Itens)
                .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(g => g.Id == id && g.UsuarioId == uid);

        if (guia is null)
            return NotFound(new { message = "Guia não encontrada." });

        // Gerar PDF da guia (usando QuestPDF)
        var pdfBytes = GerarPdfGuia(guia);
        
        return File(pdfBytes, "application/pdf", $"Guia_{guia.NumeroGuia}.pdf");
    }

    // ─── DELETE /api/user/guias/{id} ──────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteGuia(int id)
    {
        var uid = User.GetUserId();
        var guia = await _db.Guias!
            .FirstOrDefaultAsync(g => g.Id == id && g.UsuarioId == uid);

        if (guia is null)
            return NotFound(new { message = "Guia não encontrada." });

        guia.Status = "Cancelada";
        guia.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Guia cancelada com sucesso." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroGuia()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultimo = _db.Guias!
            .Where(g => g.NumeroGuia.StartsWith($"GIA/{ano}/{mes}/"))
            .OrderByDescending(g => g.NumeroGuia)
            .FirstOrDefault();

        if (ultimo is null)
            return $"GIA/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultimo.NumeroGuia.Split('/').Last());
        return $"GIA/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private GuiaResponseDto MapToResponseDto(Guia g)
    {
        return new GuiaResponseDto
        {
            Id = g.Id,
            NumeroGuia = g.NumeroGuia,
            Tipo = g.Tipo,
            Status = g.Status,
            DataEmissao = g.DataEmissao,
            AtribuicaoId = g.AtribuicaoId,
            AtribuicaoNumero = g.Atribuicao?.NumeroAtribuicao,
            ClienteId = g.ClienteId,
            ClienteNome = g.Cliente?.Nome,
            ClienteNif = g.Cliente?.Contribuinte,
            ClienteMorada = g.Cliente?.Morada,
            ClienteContacto = g.Cliente?.Telefone,
            TransportadoraId = g.TransportadoraId,
            TransportadoraNome = g.Transportadora?.Nome,
            TransportadoraNif = g.Transportadora?.Nif,
            EnderecoOrigem = g.EnderecoOrigem,
            EnderecoDestino = g.EnderecoDestino,
            TotalItens = g.TotalItens,
            PesoTotalKg = g.PesoTotalKg,
            VolumeTotalM3 = g.VolumeTotalM3,
            TotalVolumes = g.TotalVolumes,
            DataPrevistaEntrega = g.DataPrevistaEntrega,
            DataEntregaReal = g.DataEntregaReal,
            Observacoes = g.Observacoes,
            InstrucoesEspeciais = g.InstrucoesEspeciais,
            CriadoEm = g.CriadoEm,
            AtualizadoEm = g.AtualizadoEm,
            Itens = g.Itens?.Select(i => new GuiaItemResponseDto
            {
                Id = i.Id,
                ProdutoId = i.ProdutoId,
                ProdutoSku = i.Produto?.Sku,
                ProdutoNome = i.Produto?.Nome,
                Quantidade = i.Quantidade,
                PesoUnitario = i.PesoUnitario,
                PesoTotal = i.PesoTotal,
                VolumeUnitario = i.VolumeUnitario,
                VolumeTotal = i.VolumeTotal,
                Lote = i.Lote,
                Observacoes = i.Observacoes
            }).ToList() ?? []
        };
    }

    private byte[] GerarPdfGuia(Guia guia)
    {
        // Implementar geração de PDF com QuestPDF
        // Similar ao gerarPdfFatura
        return Array.Empty<byte>();
    }
}   