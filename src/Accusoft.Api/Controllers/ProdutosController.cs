using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/produtos")]
[Authorize]
public class ProdutosController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProdutosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetProdutos(
        [FromQuery] string? search,
        [FromQuery] string? categoria,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.Produtos
            .AsNoTracking()
            .Include(p => p.Fornecedor)
            .Where(p => p.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(p =>
                p.Sku.ToLower().Contains(s) ||
                p.Nome.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p =>
                p.Categoria != null &&
                p.Categoria.ToLower() == categoria.ToLower());

        if (ativo.HasValue)
            query = query.Where(p => p.Ativo == ativo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

       
        var produtoIds = items.Select(p => p.Id).ToList();
        var stockMap = await _db.Estoques
            .AsNoTracking()
            .Where(e => produtoIds.Contains(e.ProdutoId))
            .GroupBy(e => e.ProdutoId)
            .Select(g => new
            {
                ProdutoId  = g.Key,
                StockAtual = g.Sum(e => e.Quantidade - e.QuantidadeReservada)
            })
            .ToDictionaryAsync(x => x.ProdutoId, x => x.StockAtual);

        var dtos = items.Select(p => MapToResponseDto(p, stockMap)).ToList();

        return Ok(new PagedResult<ProdutoResponseDto>
        {
            Items    = dtos,
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduto(int id)
    {
        var uid     = User.GetUserId();
        var produto = await _db.Produtos
            .AsNoTracking()
            .Include(p => p.Fornecedor)
            .FirstOrDefaultAsync(p => p.Id == id && p.CriadoPor == uid);

        if (produto is null)
            return NotFound(new { message = "Produto não encontrado." });

        var stockAtual = await GetStockAtual(id);
        return Ok(MapToResponseDto(produto, new Dictionary<int, int> { { id, stockAtual } }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduto([FromBody] ProdutoCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        if (dto.StockMinimo < dto.StockInicial)
            return BadRequest(new { message = "O stock mínimo não pode ser inferior ao stock inicial." });

        var fornecedorId = dto.FornecedorId;
        if (!string.IsNullOrWhiteSpace(dto.FornecedorCodigo))
        {
            var fornecedor = await _db.FornecedoresCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Codigo == dto.FornecedorCodigo.Trim() && f.CriadoPor == uid);

            if (fornecedor is null)
                return BadRequest(new { message = "Fornecedor inválido. Código não encontrado." });

            fornecedorId = fornecedor.Id;
        }
        else if (dto.FornecedorId.HasValue)
        {
            var fornecedor = await _db.FornecedoresCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == dto.FornecedorId.Value && f.CriadoPor == uid);

            if (fornecedor is null)
                return BadRequest(new { message = "Fornecedor inválido. Código não encontrado." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Localizacao))
        {
            var armazemExists = await _db.ArmazensCatalogo
                .AsNoTracking()
                .AnyAsync(a => a.Codigo == dto.Localizacao.Trim() && a.CriadoPor == uid);

            if (!armazemExists)
                return BadRequest(new { message = "Localização do armazém inválida. Código não encontrado." });
        }

        var now = DateTimeOffset.UtcNow;
        var sku = await GetNextProdutoSku(uid);

        var produto = new Produto
        {
            Sku                 = sku,
            Nome                = dto.Nome.Trim(),
            Descricao           = dto.Descricao?.Trim(),
            Categoria           = dto.Categoria?.Trim(),
            FornecedorId        = fornecedorId,
            PrecoCompra         = dto.PrecoCompra,
            PrecoVenda          = dto.PrecoVenda,
            Iva                 = dto.Iva,
            StockAtual          = dto.StockInicial,
            StockMinimo         = dto.StockMinimo,
            UnidadeMedida       = dto.UnidadeMedida,
            Localizacao         = dto.Localizacao?.Trim(),
            LoteObrigatorio     = dto.LoteObrigatorio,
            ValidadeObrigatoria = dto.ValidadeObrigatoria,
            Ativo               = true,
            CriadoPor           = uid,
            CriadoEm            = now,
            AtualizadoEm        = now
        };

        _db.Produtos.Add(produto);
        await _db.SaveChangesAsync();

        var estoque = new Estoque
        {
            ProdutoId            = produto.Id,
            ArmazemId            = null,                        
            Localizacao          = dto.Localizacao?.Trim(),    
            Lote                 = null,                       
            Validade             = null,                    
            Quantidade           = dto.StockInicial,     
            QuantidadeReservada  = 0,                   
            QuantidadePicking    = 0,                         
            Status               = "em-estoque",
            UltimaMovimentacao   = now,
            CriadoEm             = now,
            AtualizadoEm         = now
        };
        _db.Estoques.Add(estoque);
        await _db.SaveChangesAsync();

        if (dto.StockInicial > 0)
        {
            _db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                ProdutoId    = produto.Id,
                Tipo         = MovimentacaoTipo.Entrada,
                Quantidade   = dto.StockInicial,           
                OrigemLocal  = null,
                DestinoLocal = dto.Localizacao?.Trim(),
                ArmazemId    = null,
                UsuarioId    = uid,
                Observacao   = "Stock inicial na criação do produto.",
                DataMov      = now
            });
            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetProduto), new { id = produto.Id },
            MapToResponseDto(produto, new Dictionary<int, int> { { produto.Id, dto.StockInicial } }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduto(int id, [FromBody] ProdutoUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid     = User.GetUserId();
        var produto = await _db.Produtos
            .FirstOrDefaultAsync(p => p.Id == id && p.CriadoPor == uid);

        if (produto is null)
            return NotFound(new { message = "Produto não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.FornecedorCodigo))
        {
            var fornecedor = await _db.FornecedoresCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Codigo == dto.FornecedorCodigo.Trim() && f.CriadoPor == uid);

            if (fornecedor is null)
                return BadRequest(new { message = "Fornecedor inválido. Código não encontrado." });

            produto.FornecedorId = fornecedor.Id;
        }
        else if (dto.FornecedorId.HasValue)
        {
            var fornecedor = await _db.FornecedoresCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == dto.FornecedorId.Value && f.CriadoPor == uid);

            if (fornecedor is null)
                return BadRequest(new { message = "Fornecedor inválido. Código não encontrado." });

            produto.FornecedorId = fornecedor.Id;
        }

        if (!string.IsNullOrWhiteSpace(dto.Localizacao))
        {
            var armazemExists = await _db.ArmazensCatalogo
                .AsNoTracking()
                .AnyAsync(a => a.Codigo == dto.Localizacao.Trim() && a.CriadoPor == uid);

            if (!armazemExists)
                return BadRequest(new { message = "Localização do armazém inválida. Código não encontrado." });
        }

        produto.Nome                = dto.Nome.Trim();
        produto.Descricao           = dto.Descricao?.Trim();
        produto.Categoria           = dto.Categoria?.Trim();
        produto.PrecoCompra         = dto.PrecoCompra;
        produto.PrecoVenda          = dto.PrecoVenda;
        produto.Iva                 = dto.Iva;
        produto.StockMinimo         = dto.StockMinimo;
        produto.UnidadeMedida       = dto.UnidadeMedida;
        produto.Localizacao         = dto.Localizacao?.Trim();
        produto.LoteObrigatorio     = dto.LoteObrigatorio;
        produto.ValidadeObrigatoria = dto.ValidadeObrigatoria;
        produto.Ativo               = dto.Ativo;
        produto.AtualizadoEm        = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var stockAtual = await GetStockAtual(id);
        return Ok(MapToResponseDto(produto, new Dictionary<int, int> { { id, stockAtual } }));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduto(int id)
    {
        var uid     = User.GetUserId();
        var produto = await _db.Produtos
            .FirstOrDefaultAsync(p => p.Id == id && p.CriadoPor == uid);

        if (produto is null)
            return NotFound(new { message = "Produto não encontrado." });

        var temMovimentacoes = await _db.MovimentacoesEstoque
            .AnyAsync(m => m.ProdutoId == id);

        produto.Ativo        = false;
        produto.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var msg = temMovimentacoes
            ? "Produto desativado (possui movimentações — não pode ser eliminado)."
            : "Produto desativado com sucesso.";

        return Ok(new { message = msg });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarProduto(int id)
    {
        var uid     = User.GetUserId();
        var produto = await _db.Produtos
            .FirstOrDefaultAsync(p => p.Id == id && p.CriadoPor == uid);

        if (produto is null)
            return NotFound(new { message = "Produto não encontrado." });

        produto.Ativo        = true;
        produto.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Produto ativado com sucesso." });
    }

    private async Task<int> GetStockAtual(int produtoId)
    {
        return await _db.Estoques
            .AsNoTracking()
            .Where(e => e.ProdutoId == produtoId)
            .SumAsync(e => e.Quantidade - e.QuantidadeReservada);
    }

    private async Task<string> GetNextProdutoSku(int userId)
    {
        const string prefix = "PRD-";
        var existingSkus = await _db.Produtos
            .AsNoTracking()
            .Where(p => p.CriadoPor == userId && p.Sku.StartsWith(prefix))
            .Select(p => p.Sku)
            .ToListAsync();

        var maxNumber = 0;
        foreach (var sku in existingSkus)
        {
            var parts = sku.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var number))
                maxNumber = Math.Max(maxNumber, number);
        }

        return $"{prefix}{(maxNumber + 1):D4}";
    }

    private static ProdutoResponseDto MapToResponseDto(
        Produto produto,
        Dictionary<int, int> stockMap)
    {
        return new ProdutoResponseDto
        {
            Id                  = produto.Id,
            Sku                 = produto.Sku,
            Nome                = produto.Nome,
            Descricao           = produto.Descricao,
            Categoria           = produto.Categoria,
            FornecedorId        = produto.FornecedorId,
            FornecedorCodigo    = produto.Fornecedor?.Codigo,
            FornecedorNome      = produto.Fornecedor?.Nome,
            PrecoCompra         = produto.PrecoCompra,
            PrecoVenda          = produto.PrecoVenda,
            Iva                 = produto.Iva,
            StockAtual          = stockMap.TryGetValue(produto.Id, out var s) ? s : produto.StockAtual,
            StockMinimo         = produto.StockMinimo,
            UnidadeMedida       = produto.UnidadeMedida,
            Localizacao         = produto.Localizacao,
            LoteObrigatorio     = produto.LoteObrigatorio,
            ValidadeObrigatoria = produto.ValidadeObrigatoria,
            Ativo               = produto.Ativo,
            CriadoEm            = produto.CriadoEm,
            AtualizadoEm        = produto.AtualizadoEm
        };
    }
}
