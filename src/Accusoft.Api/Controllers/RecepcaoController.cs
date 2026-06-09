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

    public RecepcaoController(AppDbContext db) => _db = db;

    private static readonly HashSet<string> StatusFinais = ["Concluida", "Cancelada"];

    [HttpGet]
    public async Task<IActionResult> GetRecepcoes(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 15)
    {
        var uid = User.GetUserId();
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.Recepcoes
            .AsNoTracking()
            .Include(r => r.Fornecedor)
            .Include(r => r.Itens)
                .ThenInclude(i => i.Produto)
            .AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(r => r.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                r.NumeroRecepcao.ToLower().Contains(s) ||
                r.Fornecedor.Nome.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.DataRecepcao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<RecepcaoResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRecepcao(int id)
    {
        var uid = User.GetUserId();
        var r   = await FindRecepcao(id, uid, includeItens: true);
        return r is null
            ? NotFound(new { message = "Receção não encontrada." })
            : Ok(MapToDto(r));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecepcao([FromBody] RecepcaoCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Erro de validação.",
                errors  = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        var uid = User.GetUserId();

        var fornecedorQuery = _db.FornecedoresCatalogo.AsNoTracking().Where(f => f.Id == dto.FornecedorId);
        if (!User.IsAdmin())
            fornecedorQuery = fornecedorQuery.Where(f => f.CriadoPor == uid);
        var fornecedorExists = await fornecedorQuery.AnyAsync();

        if (!fornecedorExists)
            return BadRequest(new { message = "Fornecedor inválido ou não pertence a este utilizador." });

        var produtoIds = dto.Itens.Select(i => i.ProdutoId).ToList();
        if (produtoIds.Distinct().Count() != produtoIds.Count)
            return BadRequest(new { message = "Não pode haver itens com o mesmo produto na mesma receção." });

        var produtoQuery = _db.Produtos
            .AsNoTracking()
            .Where(p => produtoIds.Contains(p.Id));
        if (!User.IsAdmin())
            produtoQuery = produtoQuery.Where(p => p.CriadoPor == uid);

        var produtosExistem = await produtoQuery
            .Select(p => p.Id)
            .ToListAsync();

        var inexistentes = produtoIds.Except(produtosExistem).ToList();
        if (inexistentes.Any())
            return BadRequest(new { message = $"Produto(s) inválido(s): {string.Join(", ", inexistentes)}." });

        var now    = DateTimeOffset.UtcNow;
        var numero = await GerarNumeroRecepcao(uid, now);

        var recepcao = new Recepcao
        {
            NumeroRecepcao      = numero,
            FornecedorId        = dto.FornecedorId,
            TipoEntrada         = dto.TipoEntrada,
            DataRecepcao        = DateTime.UtcNow,
            Status              = "Pendente",
            Prioridade          = dto.Prioridade,
            UsuarioId           = uid,
            CriadoEm            = now,
            AtualizadoEm        = now,
            Itens               = dto.Itens.Select(i => new RecepcaoItem
            {
                ProdutoId           = i.ProdutoId,
                QuantidadeEsperada  = i.QuantidadeEsperada,
                QuantidadeRecebida  = i.QuantidadeRecebida,
                QuantidadeRejeitada = i.QuantidadeRejeitada,
                Lote                = i.Lote?.Trim(),
                Validade            = i.Validade,
                Localizacao         = i.Localizacao?.Trim(),
                Observacoes         = i.Observacoes?.Trim(),
                Conformidade        = i.QuantidadeRejeitada == 0,
            }).ToList(),
        };

        _db.Recepcoes.Add(recepcao);
        await _db.SaveChangesAsync();

        var criada = await FindRecepcao(recepcao.Id, uid, includeItens: true);
        return CreatedAtAction(nameof(GetRecepcao), new { id = recepcao.Id }, MapToDto(criada!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRecepcao(int id, [FromBody] RecepcaoUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid      = User.GetUserId();
        var recepcao = await FindRecepcao(id, uid, includeItens: true);

        if (recepcao is null)
            return NotFound(new { message = "Receção não encontrada." });

        if (StatusFinais.Contains(recepcao.Status))
            return BadRequest(new
            {
                message = $"A receção não pode ser editada porque está '{recepcao.Status}'. " +
                           "Apenas recepções Pendentes ou Em Conferência podem ser alteradas."
            });

        if (dto.FornecedorId.HasValue)
        {
            var fornecedorQuery = _db.FornecedoresCatalogo.AsNoTracking().Where(f => f.Id == dto.FornecedorId.Value);
        if (!User.IsAdmin())
            fornecedorQuery = fornecedorQuery.Where(f => f.CriadoPor == uid);
        var exists = await fornecedorQuery.AnyAsync();
            if (!exists)
                return BadRequest(new { message = "Fornecedor inválido." });
            recepcao.FornecedorId = dto.FornecedorId.Value;
        }

        if (dto.Status is not null)         recepcao.Status              = dto.Status;
        if (dto.Prioridade is not null)     recepcao.Prioridade          = dto.Prioridade;

        if (dto.Itens is { Count: > 0 })
        {
            var prodIds = dto.Itens
                .Where(i => i.ProdutoId.HasValue)
                .Select(i => i.ProdutoId!.Value)
                .ToList();
            if (prodIds.Distinct().Count() != prodIds.Count)
                return BadRequest(new { message = "Não pode haver itens com o mesmo produto na mesma receção." });

            foreach (var itemDto in dto.Itens)
            {
                if (itemDto.Id.HasValue)
                {
                    var existingItem = recepcao.Itens.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                    if (existingItem is not null)
                    {
                        if (itemDto.QuantidadeEsperada.HasValue)  existingItem.QuantidadeEsperada  = itemDto.QuantidadeEsperada.Value;
                        if (itemDto.QuantidadeRecebida.HasValue)  existingItem.QuantidadeRecebida  = itemDto.QuantidadeRecebida.Value;
                        if (itemDto.QuantidadeRejeitada.HasValue) existingItem.QuantidadeRejeitada = itemDto.QuantidadeRejeitada.Value;
                        if (itemDto.Lote        is not null) existingItem.Lote        = itemDto.Lote.Trim();
                        if (itemDto.Validade     is not null) existingItem.Validade     = itemDto.Validade;
                        if (itemDto.Localizacao  is not null) existingItem.Localizacao  = itemDto.Localizacao.Trim();
                        if (itemDto.Observacoes  is not null) existingItem.Observacoes  = itemDto.Observacoes.Trim();
                        existingItem.Conformidade = existingItem.QuantidadeRejeitada == 0;
                    }
                }
            }
        }

        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToDto(recepcao));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRecepcao(int id)
    {
        var uid      = User.GetUserId();
        var recepcao = await FindRecepcao(id, uid);

        if (recepcao is null)
            return NotFound(new { message = "Receção não encontrada." });

        if (StatusFinais.Contains(recepcao.Status))
            return BadRequest(new
            {
                message = $"Não é possível cancelar uma receção com status '{recepcao.Status}'."
            });

        recepcao.Status       = "Cancelada";
        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Receção cancelada com sucesso." });
    }

    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirRecepcao(int id)
    {
        var uid      = User.GetUserId();
        var recepcao = await FindRecepcao(id, uid, includeItens: true);

        if (recepcao is null)
            return NotFound(new { message = "Receção não encontrada." });

        if (StatusFinais.Contains(recepcao.Status))
            return BadRequest(new
            {
                message = $"A receção já está '{recepcao.Status}' e não pode ser concluída novamente."
            });

        recepcao.Status       = "Concluida";
        recepcao.AtualizadoEm = DateTimeOffset.UtcNow;

        foreach (var item in recepcao.Itens)
        {
            var qtdAceite = item.QuantidadeRecebida - item.QuantidadeRejeitada;
            if (qtdAceite <= 0) continue;

            var estoque = await _db.Estoques
                .FirstOrDefaultAsync(e =>
                    e.ProdutoId   == item.ProdutoId &&
                    e.Localizacao == item.Localizacao);

            if (estoque is not null)
            {
                estoque.Quantidade    += qtdAceite;
                estoque.AtualizadoEm  = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.Estoques.Add(new Estoque
                {
                    ProdutoId           = item.ProdutoId,
                    Localizacao         = item.Localizacao,
                    Lote                = item.Lote,
                    Validade            = item.Validade,
                    Quantidade          = qtdAceite,
                    QuantidadeReservada = 0,
                    QuantidadePicking   = 0,
                    Status              = "em-estoque",
                    UltimaMovimentacao  = DateTimeOffset.UtcNow,
                    CriadoEm            = DateTimeOffset.UtcNow,
                    AtualizadoEm        = DateTimeOffset.UtcNow,
                });
            }

            _db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                ProdutoId    = item.ProdutoId,
                Tipo         = MovimentacaoTipo.Entrada,
                Quantidade   = qtdAceite,
                DestinoLocal = item.Localizacao,
                UsuarioId    = uid,
                Observacao   = $"Receção {recepcao.NumeroRecepcao} — lote {item.Lote ?? "—"}",
                DataMov      = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Receção concluída e stock atualizado com sucesso." });
    }


    private async Task<Recepcao?> FindRecepcao(int id, int uid, bool includeItens = false)
    {
        var query = _db.Recepcoes
            .Include(r => r.Fornecedor)
            .Where(r => r.Id == id);
        if (!User.IsAdmin())
            query = query.Where(r => r.UsuarioId == uid);

        if (includeItens)
            query = query
                .Include(r => r.Itens)
                    .ThenInclude(i => i.Produto);

        return await query.FirstOrDefaultAsync();
    }

    private async Task<string> GerarNumeroRecepcao(int userId, DateTimeOffset now)
    {
        var prefixo = $"REC-{now:yyyyMM}-";
        var existentes = await _db.Recepcoes
            .Where(r => r.UsuarioId == userId && r.NumeroRecepcao.StartsWith(prefixo))
            .Select(r => r.NumeroRecepcao)
            .ToListAsync();

        var maxSeq = 0;
        foreach (var n in existentes)
        {
            var parts = n.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
                if (seq > maxSeq) maxSeq = seq;
        }

        return $"{prefixo}{(maxSeq + 1):D3}";
    }

    private static RecepcaoResponseDto MapToDto(Recepcao r) => new()
    {
        Id                  = r.Id,
        NumeroRecepcao      = r.NumeroRecepcao,
        FornecedorId        = r.FornecedorId,
        Fornecedor          = r.Fornecedor?.Nome ?? string.Empty,
        TipoEntrada         = r.TipoEntrada,
        DataRecepcao        = r.DataRecepcao,
        Status              = r.Status,
        Prioridade          = r.Prioridade,
        TotalItens          = r.Itens?.Count ?? 0,
        TotalUnidades       = r.Itens?.Sum(i => Math.Max(0, i.QuantidadeRecebida - i.QuantidadeRejeitada)) ?? 0,
        CriadoEm            = r.CriadoEm,
        AtualizadoEm        = r.AtualizadoEm,
        Itens               = r.Itens?.Select(i => new RecepcaoItemResponseDto
        {
            Id                  = i.Id,
            ProdutoId           = i.ProdutoId,
            Sku                 = i.Produto?.Sku ?? string.Empty,
            ProdutoNome         = i.Produto?.Nome ?? string.Empty,
            QuantidadeEsperada  = i.QuantidadeEsperada,
            QuantidadeRecebida  = i.QuantidadeRecebida,
            QuantidadeRejeitada = i.QuantidadeRejeitada,
            Lote                = i.Lote,
            Validade            = i.Validade,
            Localizacao         = i.Localizacao,
            Observacoes         = i.Observacoes,
            Conformidade        = i.Conformidade,
        }).ToList() ?? [],
    };
}
