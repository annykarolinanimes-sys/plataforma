using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accusoft.Api.Models;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/rotas-catalogo")]
[Authorize]
public class RotasCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;
    public RotasCatalogoController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetRotas([FromQuery] string? search, [FromQuery] bool? ativo)
    {
        var uid = User.GetUserId();
        var query = _db.RotasCatalogo.AsNoTracking().Include(r => r.Transportadora).Where(r => r.CriadoPor == uid);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Nome.ToLower().Contains(search.ToLower()) || r.Codigo.ToLower().Contains(search.ToLower()));
        if (ativo.HasValue) query = query.Where(r => r.Ativo == ativo.Value);
        var list = await query.OrderBy(r => r.Nome).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRota(int id)
    {
        var item = await _db.RotasCatalogo.Include(r => r.Transportadora).FirstOrDefaultAsync(r => r.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRota([FromBody] RotaCatalogo rota)
    {
        var uid = User.GetUserId();
        if (string.IsNullOrWhiteSpace(rota.Codigo)) return BadRequest("Código obrigatório");
        if (string.IsNullOrWhiteSpace(rota.Nome)) return BadRequest("Nome obrigatório");
        if (await _db.RotasCatalogo.AnyAsync(r => r.Codigo == rota.Codigo && r.CriadoPor == uid))
            return Conflict("Já existe rota com este código.");
        rota.CriadoPor = uid;
        rota.CriadoEm = DateTimeOffset.UtcNow;
        rota.AtualizadoEm = DateTimeOffset.UtcNow;
        _db.RotasCatalogo.Add(rota);
        await _db.SaveChangesAsync();
        return Ok(rota);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRota(int id, [FromBody] RotaCatalogo updated)
    {
        var uid = User.GetUserId();
        var rota = await _db.RotasCatalogo.FirstOrDefaultAsync(r => r.Id == id && r.CriadoPor == uid);
        if (rota is null) return NotFound();
        if (string.IsNullOrWhiteSpace(updated.Codigo)) return BadRequest("Código obrigatório");
        if (string.IsNullOrWhiteSpace(updated.Nome)) return BadRequest("Nome obrigatório");
        if (rota.Codigo != updated.Codigo && await _db.RotasCatalogo.AnyAsync(r => r.Codigo == updated.Codigo && r.CriadoPor == uid && r.Id != id))
            return Conflict("Código já utilizado por outra rota.");
        rota.Codigo = updated.Codigo;
        rota.Nome = updated.Nome;
        rota.Descricao = updated.Descricao;
        rota.Origem = updated.Origem;
        rota.Destino = updated.Destino;
        rota.DistanciaKm = updated.DistanciaKm;
        rota.TempoEstimadoMin = updated.TempoEstimadoMin;
        rota.TransportadoraId = updated.TransportadoraId;
        rota.Ativo = updated.Ativo;
        rota.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(rota);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRota(int id)
    {
        var uid = User.GetUserId();
        var rota = await _db.RotasCatalogo.FirstOrDefaultAsync(r => r.Id == id && r.CriadoPor == uid);
        if (rota is null) return NotFound();
        rota.Ativo = false;
        rota.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Rota desativada com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarRota(int id)
    {
        var uid = User.GetUserId();
        var rota = await _db.RotasCatalogo.FirstOrDefaultAsync(r => r.Id == id && r.CriadoPor == uid);
        if (rota is null) return NotFound();
        rota.Ativo = true;
        rota.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Rota ativada com sucesso." });
    }
}