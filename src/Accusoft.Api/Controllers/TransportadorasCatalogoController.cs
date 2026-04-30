using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/transportadoras-catalogo")]
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
        [FromQuery] bool? ativo)
    {
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

        var transportadoras = await query.OrderBy(t => t.Nome).ToListAsync();
        return Ok(transportadoras);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTransportadora(int id)
    {
        var transportadora = await _db.TransportadorasCatalogo
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        return Ok(transportadora);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransportadora([FromBody] TransportadoraCatalogo transportadora)
    {
        var uid = User.GetUserId();

        if (string.IsNullOrWhiteSpace(transportadora.Codigo))
            return BadRequest(new { message = "Código da transportadora é obrigatório." });
        if (string.IsNullOrWhiteSpace(transportadora.Nome))
            return BadRequest(new { message = "Nome da transportadora é obrigatório." });

        if (await _db.TransportadorasCatalogo.AnyAsync(t => t.Codigo == transportadora.Codigo && t.CriadoPor == uid))
            return Conflict(new { message = "Já existe uma transportadora com este código." });

        transportadora.CriadoPor = uid;
        transportadora.CriadoEm = DateTimeOffset.UtcNow;
        transportadora.AtualizadoEm = DateTimeOffset.UtcNow;

        _db.TransportadorasCatalogo.Add(transportadora);
        await _db.SaveChangesAsync();

        return Ok(transportadora);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTransportadora(int id, [FromBody] TransportadoraCatalogo updated)
    {
        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo.FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        if (string.IsNullOrWhiteSpace(updated.Codigo))
            return BadRequest(new { message = "Código da transportadora é obrigatório." });
        if (string.IsNullOrWhiteSpace(updated.Nome))
            return BadRequest(new { message = "Nome da transportadora é obrigatório." });

        if (transportadora.Codigo != updated.Codigo &&
            await _db.TransportadorasCatalogo.AnyAsync(t => t.Codigo == updated.Codigo && t.CriadoPor == uid && t.Id != id))
            return Conflict(new { message = "Já existe outra transportadora com este código." });

        transportadora.Codigo = updated.Codigo;
        transportadora.Nome = updated.Nome;
        transportadora.Nif = updated.Nif;
        transportadora.Telefone = updated.Telefone;
        transportadora.Email = updated.Email;
        transportadora.Morada = updated.Morada;
        transportadora.Localidade = updated.Localidade;
        transportadora.CodigoPostal = updated.CodigoPostal;
        transportadora.Pais = updated.Pais;
        transportadora.ContactoNome = updated.ContactoNome;
        transportadora.ContactoTelefone = updated.ContactoTelefone;
        transportadora.Observacoes = updated.Observacoes;
        transportadora.Ativo = updated.Ativo;
        transportadora.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(transportadora);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransportadora(int id)
    {
        var uid = User.GetUserId();
        var transportadora = await _db.TransportadorasCatalogo.FirstOrDefaultAsync(t => t.Id == id && t.CriadoPor == uid);

        if (transportadora is null)
            return NotFound(new { message = "Transportadora não encontrada." });

        transportadora.Ativo = false;
        transportadora.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Transportadora desativada com sucesso." });
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
}