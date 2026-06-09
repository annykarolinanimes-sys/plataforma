using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/clientes-catalogo")]
[Authorize]
public class ClientesCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientesCatalogoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetClientes(
        [FromQuery] string? search,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.ClientesCatalogo
            .AsNoTracking();

        if (!User.IsAdmin())
        {
            query = query.Where(c => c.CriadoPor == uid);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            var idFiltro = int.TryParse(search.Trim(), out var parsedId) ? parsedId : (int?)null;

            query = query.Where(c =>
                (idFiltro.HasValue && c.Id == idFiltro.Value) ||
                (c.Nome != null && c.Nome.ToLower().Contains(s)) ||
                (c.Codigo != null && c.Codigo.ToLower().Contains(s)) ||
                (c.Contribuinte != null && c.Contribuinte.ToLower().Contains(s)) ||
                (c.Telefone != null && c.Telefone.ToLower().Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)) ||
                (c.Morada != null && c.Morada.ToLower().Contains(s)) ||
                (c.Localidade != null && c.Localidade.ToLower().Contains(s)) ||
                (c.CodigoPostal != null && c.CodigoPostal.ToLower().Contains(s)) ||
                (c.Pais != null && c.Pais.ToLower().Contains(s)) ||
                (c.ContactoNome != null && c.ContactoNome.ToLower().Contains(s)) ||
                (c.ContactoTelefone != null && c.ContactoTelefone.ToLower().Contains(s)) ||
                (c.Observacoes != null && c.Observacoes.ToLower().Contains(s)));
        }

        if (ativo.HasValue)
            query = query.Where(c => c.Ativo == ativo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<ClienteResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCliente(int id)
    {
        var uid     = User.GetUserId();
        var query = _db.ClientesCatalogo.AsNoTracking();
        if (!User.IsAdmin())
            query = query.Where(c => c.CriadoPor == uid);

        var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        return Ok(MapToDto(cliente));
    }

    [HttpGet("next")]
    public async Task<IActionResult> GetNextClienteCodigoEndpoint()
    {
        var uid = User.GetUserId();
        var codigo = await GetNextClienteCodigo(uid);
        return Ok(new { code = codigo });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCliente([FromBody] ClienteCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        if (!string.IsNullOrWhiteSpace(dto.Contribuinte))
        {
            var normalizedContribuinte = dto.Contribuinte.Trim().ToLowerInvariant();
            if (await _db.ClientesCatalogo.AnyAsync(c =>
                    c.Contribuinte != null && c.Contribuinte.ToLower() == normalizedContribuinte))
                return Conflict(new { message = "Já existe um cliente com este contribuinte (NIF)." });

            if (await _db.FornecedoresCatalogo.AnyAsync(f =>
                    f.Nif != null && f.Nif.ToLower() == normalizedContribuinte))
                return Conflict(new { message = "Já existe um fornecedor com este NIF." });

            if (await _db.Motoristas.AnyAsync(m =>
                    m.Nif != null && m.Nif.ToLower() == normalizedContribuinte))
                return Conflict(new { message = "Já existe um motorista com este NIF." });
        }

        var codigoGerado = await GetNextClienteCodigo(uid);
        var now = DateTimeOffset.UtcNow;

        var cliente = new ClienteCatalogo
        {
            Codigo            = codigoGerado,
            Nome              = dto.Nome.Trim(),
            Contribuinte      = dto.Contribuinte?.Trim(),
            Telefone          = dto.Telefone?.Trim(),
            Email             = dto.Email?.Trim().ToLower(),
            Morada            = dto.Morada?.Trim(),
            Localidade        = dto.Localidade?.Trim(),
            CodigoPostal      = dto.CodigoPostal?.Trim(),
            Pais              = dto.Pais?.Trim() ?? "Portugal",
            ContactoNome      = dto.ContactoNome?.Trim(),
            ContactoTelefone  = dto.ContactoTelefone?.Trim(),
            Observacoes       = dto.Observacoes?.Trim(),
            Ativo             = true,
            CriadoPor         = uid,
            CriadoEm          = now,
            AtualizadoEm      = now
        };

        _db.ClientesCatalogo.Add(cliente);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, MapToDto(cliente));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCliente(int id, [FromBody] ClienteUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var query = _db.ClientesCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(c => c.CriadoPor == uid);

        var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Contribuinte))
        {
            var normalizedContribuinte = dto.Contribuinte.Trim().ToLowerInvariant();
            var currentContribuinte = cliente.Contribuinte?.Trim().ToLowerInvariant();

            if (currentContribuinte != normalizedContribuinte)
            {
                if (await _db.ClientesCatalogo.AnyAsync(c =>
                        c.Contribuinte != null && c.Contribuinte.ToLower() == normalizedContribuinte && c.Id != id))
                    return Conflict(new { message = "Já existe outro cliente com este contribuinte (NIF)." });

                if (await _db.FornecedoresCatalogo.AnyAsync(f =>
                        f.Nif != null && f.Nif.ToLower() == normalizedContribuinte))
                    return Conflict(new { message = "Já existe um fornecedor com este NIF." });

                if (await _db.Motoristas.AnyAsync(m =>
                        m.Nif != null && m.Nif.ToLower() == normalizedContribuinte))
                    return Conflict(new { message = "Já existe um motorista com este NIF." });
            }
        }

        cliente.Nome             = dto.Nome.Trim();
        cliente.Contribuinte     = dto.Contribuinte?.Trim();
        cliente.Telefone         = dto.Telefone?.Trim();
        cliente.Email            = dto.Email?.Trim().ToLower();
        cliente.Morada           = dto.Morada?.Trim();
        cliente.Localidade       = dto.Localidade?.Trim();
        cliente.CodigoPostal     = dto.CodigoPostal?.Trim();
        cliente.Pais             = dto.Pais?.Trim() ?? "Portugal";
        cliente.ContactoNome     = dto.ContactoNome?.Trim();
        cliente.ContactoTelefone = dto.ContactoTelefone?.Trim();
        cliente.Observacoes      = dto.Observacoes?.Trim();
        cliente.Ativo            = dto.Ativo;
        cliente.AtualizadoEm     = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(cliente));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var uid = User.GetUserId();
        var query = _db.ClientesCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(c => c.CriadoPor == uid);

        var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        cliente.Ativo        = false;
        cliente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Cliente desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarCliente(int id)
    {
        var uid = User.GetUserId();
        var query = _db.ClientesCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(c => c.CriadoPor == uid);

        var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        cliente.Ativo        = true;
        cliente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Cliente ativado com sucesso." });
    }

    private static ClienteResponseDto MapToDto(ClienteCatalogo c) => new()
    {
        Id                = c.Id,
        Codigo            = c.Codigo,
        Nome              = c.Nome,
        Contribuinte      = c.Contribuinte,
        Telefone          = c.Telefone,
        Email             = c.Email,
        Morada            = c.Morada,
        Localidade        = c.Localidade,
        CodigoPostal      = c.CodigoPostal,
        Pais              = c.Pais,
        ContactoNome      = c.ContactoNome,
        ContactoTelefone  = c.ContactoTelefone,
        Observacoes       = c.Observacoes,
        Ativo             = c.Ativo,
        CriadoEm          = c.CriadoEm,
        AtualizadoEm      = c.AtualizadoEm
    };

    private async Task<string> GetNextClienteCodigo(int userId)
    {
        const string prefix = "CLI-";
        var existingCodes = await _db.ClientesCatalogo
            .Where(c => c.Codigo.StartsWith(prefix))
            .Select(c => c.Codigo)
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
