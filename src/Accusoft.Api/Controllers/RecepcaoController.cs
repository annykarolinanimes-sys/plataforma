using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/recepcao")]
[Authorize]
public class RecepcaoController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecepcaoController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/recepcao ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetRecepcoes(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.Rececoes!
            .AsNoTracking()
            .Include(r => r.Itens)
            .Where(r => r.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                r.NumeroRecepcao.ToLower().Contains(s) ||
                r.Fornecedor.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.DataRecepcao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<RecepcaoResponseDto>
        {
            Items = items.Select(MapToResponseDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/recepcao/{id} ───────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRecepcao(int id)
    {
        var uid = User.GetUserId();
        var recepcao = await _db.Rececoes!
            .AsNoTracking()
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == uid);

        if (recepcao is null)
            return NotFound(new { message = "Recepção não encontrada." });

        return Ok(MapToResponseDto(recepcao));
    }

    // ─── POST /api/user/recepcao ───────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateRecepcao([FromBody] RecepcaoCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        var recepcao = new Recepcao
        {
            NumeroRecepcao = GerarNumeroRecepcao(),
            Fornecedor = dto.Fornecedor.Trim(),
            TipoEntrada = dto.TipoEntrada,
            DataRecepcao = DateTime.UtcNow,
            Status = "Pendente",
            Prioridade = dto.Prioridade,
            DocumentoReferencia = dto.DocumentoReferencia?.Trim(),
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.Rececoes!.Add(recepcao);
        await _db.SaveChangesAsync();

        if (dto.Itens != null && dto.Itens.Any())
        {
            foreach (var item in dto.Itens)
            {
                var conformidade = item.QuantidadeRejeitada == 0 && 
                                   item.QuantidadeRecebida == item.QuantidadeEsperada;

                _db.RecepcaoItens!.Add(new RecepcaoItem
                {
                    RecepcaoId = recepcao.Id,
                    Sku = item.Sku.Trim(),
                    ProdutoNome = item.ProdutoNome.Trim(),
                    QuantidadeEsperada = item.QuantidadeEsperada,
                    QuantidadeRecebida = item.QuantidadeRecebida,
                    QuantidadeRejeitada = item.QuantidadeRejeitada,
                    Lote = item.Lote?.Trim(),
                    Validade = item.Validade,
                    Localizacao = item.Localizacao?.Trim(),
                    Observacoes = item.Observacoes?.Trim(),
                    Conformidade = conformidade
                });
            }

            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetRecepcao), new { id = recepcao.Id }, MapToResponseDto(recepcao));
    }

    // ─── PUT /api/user/recepcao/{id} ───────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRecepcao(int id, [FromBody] RecepcaoUpdateDto dto)
    {
        var uid = User.GetUserId();
        var recepcao = await _db.Rececoes!
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == uid);

        if (recepcao is null)
            return NotFound(new { message = "Recepção não encontrada." });

        if (!string.IsNullOrWhiteSpace(dto.Fornecedor))
            recepcao.Fornecedor = dto.Fornecedor.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Status))
            recepcao.Status = dto.Status;

        if (!string.IsNullOrWhiteSpace(dto.Prioridade))
            recepcao.Prioridade = dto.Prioridade;

        if (!string.IsNullOrWhiteSpace(dto.DocumentoReferencia))
            recepcao.DocumentoReferencia = dto.DocumentoReferencia.Trim();

        if (dto.Itens is not null && dto.Itens.Any())
        {
            // Remover itens eliminados
            var idsToKeep = dto.Itens.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToList();
            var itensToRemove = recepcao.Itens!.Where(i => !idsToKeep.Contains(i.Id)).ToList();
            _db.RecepcaoItens!.RemoveRange(itensToRemove);

            // Atualizar ou adicionar itens
            foreach (var itemDto in dto.Itens)
            {
                if (itemDto.Id.HasValue)
                {
                    var existingItem = recepcao.Itens!.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                    if (existingItem != null)
                    {
                        if (!string.IsNullOrWhiteSpace(itemDto.Sku))
                            existingItem.Sku = itemDto.Sku.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(itemDto.ProdutoNome))
                            existingItem.ProdutoNome = itemDto.ProdutoNome.Trim();
                        
                        if (itemDto.QuantidadeEsperada.HasValue)
                            existingItem.QuantidadeEsperada = itemDto.QuantidadeEsperada.Value;
                        
                        if (itemDto.QuantidadeRecebida.HasValue)
                            existingItem.QuantidadeRecebida = itemDto.QuantidadeRecebida.Value;
                        
                        if (itemDto.QuantidadeRejeitada.HasValue)
                            existingItem.QuantidadeRejeitada = itemDto.QuantidadeRejeitada.Value;
                        
                        if (!string.IsNullOrWhiteSpace(itemDto.Lote))
                            existingItem.Lote = itemDto.Lote.Trim();
                        
                        if (itemDto.Validade.HasValue)
                            existingItem.Validade = itemDto.Validade.Value;
                        
                        if (!string.IsNullOrWhiteSpace(itemDto.Localizacao))
                            existingItem.Localizacao = itemDto.Localizacao.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(itemDto.Observacoes))
                            existingItem.Observacoes = itemDto.Observacoes.Trim();
                        
                        existingItem.Conformidade = existingItem.QuantidadeRejeitada == 0 && 
                                                    existingItem.QuantidadeRecebida == existingItem.QuantidadeEsperada;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(itemDto.Sku) && 
                         !string.IsNullOrWhiteSpace(itemDto.ProdutoNome) && 
                         itemDto.QuantidadeEsperada.HasValue)
                {
                    _db.RecepcaoItens!.Add(new RecepcaoItem
                    {
                        RecepcaoId = recepcao.Id,
                        Sku = itemDto.Sku.Trim(),
                        ProdutoNome = itemDto.ProdutoNome.Trim(),
                        QuantidadeEsperada = itemDto.QuantidadeEsperada.Value,
                        QuantidadeRecebida = itemDto.QuantidadeRecebida ?? 0,
                        QuantidadeRejeitada = itemDto.QuantidadeRejeitada ?? 0,
                        Lote = itemDto.Lote?.Trim(),
                        Validade = itemDto.Validade,
                        Localizacao = itemDto.Localizacao?.Trim(),
                        Observacoes = itemDto.Observacoes?.Trim(),
                        Conformidade = (itemDto.QuantidadeRejeitada ?? 0) == 0 && 
                                       (itemDto.QuantidadeRecebida ?? 0) == (itemDto.QuantidadeEsperada ?? 0)
                    });
                }
            }
        }

        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(recepcao));
    }

    // ─── DELETE /api/user/recepcao/{id} (Soft Delete) ──────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRecepcao(int id)
    {
        var uid = User.GetUserId();
        var recepcao = await _db.Rececoes!
            .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == uid);

        if (recepcao is null)
            return NotFound(new { message = "Recepção não encontrada." });

        recepcao.Status = "Cancelada";
        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Recepção cancelada com sucesso." });
    }

    // ─── POST /api/user/recepcao/{id}/concluir ─────────────────────────────────
    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirRecepcao(int id)
    {
        var uid = User.GetUserId();
        var recepcao = await _db.Rececoes!
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == uid);

        if (recepcao is null)
            return NotFound(new { message = "Recepção não encontrada." });

        if (recepcao.Status != "Pendente" && recepcao.Status != "EmConferencia")
            return BadRequest(new { message = "Apenas recepções pendentes podem ser concluídas." });

        recepcao.Status = "Concluida";
        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Recepção concluída com sucesso." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroRecepcao()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultima = _db.Rececoes!
            .Where(r => r.NumeroRecepcao.StartsWith($"REC/{ano}/{mes}/"))
            .OrderByDescending(r => r.NumeroRecepcao)
            .FirstOrDefault();

        if (ultima is null)
            return $"REC/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultima.NumeroRecepcao.Split('/').Last());
        return $"REC/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private static RecepcaoResponseDto MapToResponseDto(Recepcao r)
    {
        return new RecepcaoResponseDto
        {
            Id = r.Id,
            NumeroRecepcao = r.NumeroRecepcao,
            Fornecedor = r.Fornecedor,
            TipoEntrada = r.TipoEntrada,
            DataRecepcao = r.DataRecepcao,
            Status = r.Status,
            Prioridade = r.Prioridade,
            DocumentoReferencia = r.DocumentoReferencia,
            TotalItens = r.Itens?.Count ?? 0,
            TotalUnidades = r.Itens?.Sum(i => i.QuantidadeRecebida - i.QuantidadeRejeitada) ?? 0,
            CriadoEm = r.CriadoEm,
            AtualizadoEm = r.AtualizadoEm,
            Itens = r.Itens?.Select(i => new RecepcaoItemResponseDto
            {
                Id = i.Id,
                Sku = i.Sku,
                ProdutoNome = i.ProdutoNome,
                QuantidadeEsperada = i.QuantidadeEsperada,
                QuantidadeRecebida = i.QuantidadeRecebida,
                QuantidadeRejeitada = i.QuantidadeRejeitada,
                Lote = i.Lote,
                Validade = i.Validade,
                Localizacao = i.Localizacao,
                Observacoes = i.Observacoes,
                Conformidade = i.Conformidade
            }).ToList() ?? new List<RecepcaoItemResponseDto>()
        };
    }
}