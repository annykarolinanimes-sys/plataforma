using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/fornecedores-catalogo")]
[Authorize]
public class FornecedoresCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public FornecedoresCatalogoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetFornecedores(
        [FromQuery] string? search,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.FornecedoresCatalogo
            .AsNoTracking()
            .Where(f => f.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(f =>
                f.Nome.ToLower().Contains(s) ||
                f.Codigo.ToLower().Contains(s) ||
                (f.Nif    != null && f.Nif.Contains(s)) ||
                (f.Email  != null && f.Email.ToLower().Contains(s)));
        }

        if (ativo.HasValue)
            query = query.Where(f => f.Ativo == ativo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<FornecedorResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFornecedor(int id)
    {
        var uid        = User.GetUserId();
        var fornecedor = await _db.FornecedoresCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.CriadoPor == uid);

        if (fornecedor is null)
            return NotFound(new { message = "Fornecedor não encontrado." });

        return Ok(MapToDto(fornecedor));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFornecedor([FromBody] FornecedorCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        if (!string.IsNullOrWhiteSpace(dto.Nif) &&
            await _db.FornecedoresCatalogo.AnyAsync(f =>
                f.Nif == dto.Nif.Trim() && f.CriadoPor == uid))
            return Conflict(new { message = "Já existe um fornecedor com este NIF." });

        var codigoGerado = await GetNextFornecedorCodigo(uid);
        var now = DateTimeOffset.UtcNow;

        var fornecedor = new FornecedorCatalogo
        {
            Codigo           = codigoGerado,
            Nome             = dto.Nome.Trim(),
            Nif              = dto.Nif?.Trim(),
            Telefone         = dto.Telefone?.Trim(),
            Email            = dto.Email?.Trim().ToLower(),
            Morada           = dto.Morada?.Trim(),
            Localidade       = dto.Localidade?.Trim(),
            CodigoPostal     = dto.CodigoPostal?.Trim(),
            Pais             = dto.Pais?.Trim() ?? "Portugal",
            ContactoNome     = dto.ContactoNome?.Trim(),
            ContactoTelefone = dto.ContactoTelefone?.Trim(),
            Observacoes      = dto.Observacoes?.Trim(),
            Ativo            = true,
            CriadoPor        = uid,
            CriadoEm        = now,
            AtualizadoEm    = now
        };

        _db.FornecedoresCatalogo.Add(fornecedor);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFornecedor), new { id = fornecedor.Id }, MapToDto(fornecedor));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFornecedor(int id, [FromBody] FornecedorUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid        = User.GetUserId();
        var fornecedor = await _db.FornecedoresCatalogo
            .FirstOrDefaultAsync(f => f.Id == id && f.CriadoPor == uid);

        if (fornecedor is null)
            return NotFound(new { message = "Fornecedor não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nif) &&
            fornecedor.Nif != dto.Nif.Trim() &&
            await _db.FornecedoresCatalogo.AnyAsync(f =>
                f.Nif == dto.Nif.Trim() && f.CriadoPor == uid && f.Id != id))
            return Conflict(new { message = "Já existe outro fornecedor com este NIF." });

        fornecedor.Nome             = dto.Nome.Trim();
        fornecedor.Nif              = dto.Nif?.Trim();
        fornecedor.Telefone         = dto.Telefone?.Trim();
        fornecedor.Email            = dto.Email?.Trim().ToLower();
        fornecedor.Morada           = dto.Morada?.Trim();
        fornecedor.Localidade       = dto.Localidade?.Trim();
        fornecedor.CodigoPostal     = dto.CodigoPostal?.Trim();
        fornecedor.Pais             = dto.Pais?.Trim() ?? "Portugal";
        fornecedor.ContactoNome     = dto.ContactoNome?.Trim();
        fornecedor.ContactoTelefone = dto.ContactoTelefone?.Trim();
        fornecedor.Observacoes      = dto.Observacoes?.Trim();
        fornecedor.Ativo            = dto.Ativo;
        fornecedor.AtualizadoEm     = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(fornecedor));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFornecedor(int id)
    {
        var uid        = User.GetUserId();
        var fornecedor = await _db.FornecedoresCatalogo
            .FirstOrDefaultAsync(f => f.Id == id && f.CriadoPor == uid);

        if (fornecedor is null)
            return NotFound(new { message = "Fornecedor não encontrado." });

        var temProdutos = await _db.Produtos.AnyAsync(p => p.FornecedorId == id);

        fornecedor.Ativo        = false;
        fornecedor.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var msg = temProdutos
            ? "Fornecedor desativado (possui produtos associados)."
            : "Fornecedor desativado com sucesso.";

        return Ok(new { message = msg });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarFornecedor(int id)
    {
        var uid        = User.GetUserId();
        var fornecedor = await _db.FornecedoresCatalogo
            .FirstOrDefaultAsync(f => f.Id == id && f.CriadoPor == uid);

        if (fornecedor is null)
            return NotFound(new { message = "Fornecedor não encontrado." });

        fornecedor.Ativo        = true;
        fornecedor.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Fornecedor ativado com sucesso." });
    }

    private static FornecedorResponseDto MapToDto(FornecedorCatalogo f) => new()
    {
        Id                = f.Id,
        Codigo            = f.Codigo,
        Nome              = f.Nome,
        Nif               = f.Nif,
        Telefone          = f.Telefone,
        Email             = f.Email,
        Morada            = f.Morada,
        Localidade        = f.Localidade,
        CodigoPostal      = f.CodigoPostal,
        Pais              = f.Pais,
        ContactoNome      = f.ContactoNome,
        ContactoTelefone  = f.ContactoTelefone,
        Observacoes       = f.Observacoes,
        Ativo             = f.Ativo,
        CriadoEm         = f.CriadoEm,
        AtualizadoEm     = f.AtualizadoEm
    };

    private async Task<string> GetNextFornecedorCodigo(int userId)
    {
        const string prefix = "FOR-";
        var existingCodes = await _db.FornecedoresCatalogo
            .Where(f => f.CriadoPor == userId && f.Codigo.StartsWith(prefix))
            .Select(f => f.Codigo)
            .ToListAsync();

        var maxNumber = 0;
        foreach (var code in existingCodes)
        {
            var parts = code.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var number))
                maxNumber = Math.Max(maxNumber, number);
        }

        return $"{prefix}{(maxNumber + 1):D3}";
    }
}
