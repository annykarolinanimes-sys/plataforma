using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/armazens")]
[Authorize]
public class ArmazensController : ControllerBase
{
    private readonly AppDbContext _db;

    public ArmazensController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetArmazens(
        [FromQuery] string? search,
        [FromQuery] bool? ativo)
    {
        var uid = User.GetUserId();
        var query = _db.ArmazensCatalogo
            .AsNoTracking()
            .Where(a => a.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                a.Nome.ToLower().Contains(search.ToLower()) ||
                (a.Codigo != null && a.Codigo.ToLower().Contains(search.ToLower())));

        if (ativo.HasValue)
            query = query.Where(a => a.Ativo == ativo.Value);

        var armazens = await query.OrderBy(a => a.Nome).ToListAsync();
        return Ok(armazens);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetArmazem(int id)
    {
        var armazem = await _db.ArmazensCatalogo.FirstOrDefaultAsync(a => a.Id == id);
        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });
        return Ok(armazem);
    }

    [HttpPost]
    public async Task<IActionResult> CreateArmazem([FromBody] Armazem armazem)
    {
        var uid = User.GetUserId();

        if (string.IsNullOrWhiteSpace(armazem.Codigo))
            return BadRequest(new { message = "Código do armazém é obrigatório." });
        if (string.IsNullOrWhiteSpace(armazem.Nome))
            return BadRequest(new { message = "Nome do armazém é obrigatório." });

        if (await _db.ArmazensCatalogo.AnyAsync(a => a.Codigo == armazem.Codigo && a.CriadoPor == uid))
            return Conflict(new { message = "Já existe um armazém com este código." });

        armazem.CriadoPor = uid;
        armazem.CriadoEm = DateTimeOffset.UtcNow;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;

        _db.ArmazensCatalogo.Add(armazem);
        await _db.SaveChangesAsync();

        return Ok(armazem);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateArmazem(int id, [FromBody] Armazem updated)
    {
        var uid = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo.FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        if (string.IsNullOrWhiteSpace(updated.Codigo))
            return BadRequest(new { message = "Código do armazém é obrigatório." });
        if (string.IsNullOrWhiteSpace(updated.Nome))
            return BadRequest(new { message = "Nome do armazém é obrigatório." });

        if (armazem.Codigo != updated.Codigo &&
            await _db.ArmazensCatalogo.AnyAsync(a => a.Codigo == updated.Codigo && a.CriadoPor == uid && a.Id != id))
            return Conflict(new { message = "Já existe outro armazém com este código." });

        armazem.Codigo = updated.Codigo;
        armazem.Nome = updated.Nome;
        armazem.Tipo = updated.Tipo;
        armazem.Morada = updated.Morada;
        armazem.Localidade = updated.Localidade;
        armazem.CodigoPostal = updated.CodigoPostal;
        armazem.Pais = updated.Pais;
        armazem.Telefone = updated.Telefone;
        armazem.Email = updated.Email;
        armazem.ResponsavelNome = updated.ResponsavelNome;
        armazem.ResponsavelTelefone = updated.ResponsavelTelefone;
        armazem.Observacoes = updated.Observacoes;
        armazem.Ativo = updated.Ativo;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(armazem);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteArmazem(int id)
    {
        var uid = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo.FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        armazem.Ativo = false;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarArmazem(int id)
    {
        var uid = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo.FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        armazem.Ativo = true;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém ativado com sucesso." });
    }
}