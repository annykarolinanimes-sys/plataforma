using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/transportadoras")]
[Authorize]
public class TransportadorasCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public TransportadorasCatalogoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransportadoras(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string orderBy = "nome",
        [FromQuery] string orderDir = "asc")
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();
        var query = _db.TransportadorasCatalogo
            .AsNoTracking()
            .Where(t => t.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t =>
                t.Nome.ToLower().Contains(search.ToLower()) ||
                (t.Codigo != null && t.Codigo.ToLower().Contains(search.ToLower())) ||
                (t.Nif != null && t.Nif.Contains(search)));

        if (ativo.HasValue)
            query = query.Where(t => t.Ativo == ativo.Value);

        var total = await query.CountAsync();

        IQueryable<TransportadoraCatalogo> orderedQuery = orderBy.ToLower() switch
        {
            "codigo" => orderDir.ToLower() == "desc" ? query.OrderByDescending(t => t.Codigo) : query.OrderBy(t => t.Codigo),
            _ => orderDir.ToLower() == "desc" ? query.OrderByDescending(t => t.Nome) : query.OrderBy(t => t.Nome)
        };

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<TransportadoraResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTransportadora(int id)
    {
        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        return Ok(MapToDto(transportadora));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransportadora([FromBody] TransportadoraCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        if (!string.IsNullOrWhiteSpace(dto.Nif) &&
            await _db.TransportadorasCatalogo.AnyAsync(t =>
                t.Nif == dto.Nif.Trim() && t.CriadoPor == uid))
            return Conflict(new { message = "Já existe uma transportadora com este NIF." });

        var codigoGerado = await GetNextTransportadoraCodigo(uid);
        var now = DateTimeOffset.UtcNow;

        var transportadora = new TransportadoraCatalogo
        {
            Codigo = codigoGerado,
            Nome = dto.Nome.Trim(),
            Nif = dto.Nif?.Trim(),
            Telefone = dto.Telefone?.Trim(),
            Email = dto.Email?.Trim(),
            Localidade = dto.Localidade?.Trim(),
            CodigoPostal = dto.CodigoPostal?.Trim(),
            Pais = dto.Pais?.Trim() ?? "Portugal",
            ContactoNome = dto.ContactoNome?.Trim(),
            ContactoTelefone = dto.ContactoTelefone?.Trim(),
            Observacoes = dto.Observacoes?.Trim(),
            Ativo = true,
            CriadoPor = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.TransportadorasCatalogo.Add(transportadora);
        await _db.SaveChangesAsync();

        return Ok(MapToDto(transportadora));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTransportadora(int id, [FromBody] TransportadoraUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo.FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        if (!string.IsNullOrWhiteSpace(dto.Codigo) &&
            transportadora.Codigo != dto.Codigo.Trim() &&
            await _db.TransportadorasCatalogo.AnyAsync(t => t.Codigo == dto.Codigo.Trim() && t.CriadoPor == uid && t.Id != id))
            return Conflict(new { message = "Já existe outra transportadora com este código." });

        if (!string.IsNullOrWhiteSpace(dto.Nif) &&
            transportadora.Nif != dto.Nif.Trim() &&
            await _db.TransportadorasCatalogo.AnyAsync(t => t.Nif == dto.Nif.Trim() && t.CriadoPor == uid && t.Id != id))
            return Conflict(new { message = "Já existe outra transportadora com este NIF." });

        transportadora.Codigo = dto.Codigo?.Trim() ?? transportadora.Codigo;
        transportadora.Nome = dto.Nome.Trim();
        transportadora.Nif = dto.Nif?.Trim();
        transportadora.Telefone = dto.Telefone?.Trim();
        transportadora.Email = dto.Email?.Trim();
        transportadora.Localidade = dto.Localidade?.Trim();
        transportadora.CodigoPostal = dto.CodigoPostal?.Trim();
        transportadora.Pais = dto.Pais?.Trim();
        transportadora.ContactoNome = dto.ContactoNome?.Trim();
        transportadora.ContactoTelefone = dto.ContactoTelefone?.Trim();
        transportadora.Observacoes = dto.Observacoes?.Trim();
        transportadora.Ativo = dto.Ativo;
        transportadora.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(MapToDto(transportadora));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransportadora(int id)
    {
        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo.FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        _db.TransportadorasCatalogo.Remove(transportadora);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Transportadora removida com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarTransportadora(int id)
    {
        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo.FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        transportadora.Ativo = true;
        transportadora.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Transportadora ativada com sucesso." });
    }

    private async Task<string> GetNextTransportadoraCodigo(int uid)
    {
        var lastCode = await _db.TransportadorasCatalogo
            .Where(t => t.CriadoPor == uid)
            .OrderByDescending(t => t.Id)
            .Select(t => t.Codigo)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith("TR"))
            return "TR001";

        var numberPart = lastCode.Substring(2);
        if (int.TryParse(numberPart, out var num))
            return $"TR{(num + 1):D3}";

        return "TR001";
    }

    private static TransportadoraResponseDto MapToDto(TransportadoraCatalogo t) => new()
    {
        Id = t.Id,
        Codigo = t.Codigo,
        Nome = t.Nome,
        Nif = t.Nif,
        Telefone = t.Telefone,
        Email = t.Email,
        Localidade = t.Localidade,
        CodigoPostal = t.CodigoPostal,
        Pais = t.Pais,
        ContactoNome = t.ContactoNome,
        ContactoTelefone = t.ContactoTelefone,
        Observacoes = t.Observacoes,
        Ativo = t.Ativo,
        CriadoEm = t.CriadoEm,
        AtualizadoEm = t.AtualizadoEm
    };
}