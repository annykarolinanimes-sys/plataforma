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

    // Método auxiliar para gerar código único
    private async Task<string> GerarCodigoUnicoAsync(int userId)
    {
        var prefix = "ARM";
        var lastNumber = 0;
        
        // Buscar o último código criado pelo usuário
        var ultimoCodigo = await _db.ArmazensCatalogo
            .Where(a => a.CriadoPor == userId && a.Codigo.StartsWith(prefix))
            .OrderByDescending(a => a.Id)
            .Select(a => a.Codigo)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(ultimoCodigo))
        {
            var parts = ultimoCodigo.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var num))
            {
                lastNumber = num;
            }
        }

        var novoNumero = (lastNumber + 1).ToString("D4");
        return $"{prefix}-{novoNumero}";
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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        // Se o código foi fornecido, validar unicidade
        if (!string.IsNullOrWhiteSpace(armazem.Codigo))
        {
            if (await _db.ArmazensCatalogo.AnyAsync(a => a.Codigo == armazem.Codigo.Trim() && a.CriadoPor == uid))
                return Conflict(new { message = "Já existe um armazém com este código." });
        }

        // Validar unicidade da localização
        if (await _db.ArmazensCatalogo.AnyAsync(a => a.Localizacao == armazem.Localizacao.Trim() && a.CriadoPor == uid))
            return Conflict(new { message = "Já existe um armazém com esta localização." });

        armazem.CriadoPor = uid;
        armazem.CriadoEm = DateTimeOffset.UtcNow;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;

        // Gerar código automaticamente se não foi fornecido
        if (string.IsNullOrWhiteSpace(armazem.Codigo))
        {
            armazem.Codigo = await GerarCodigoUnicoAsync(uid);
        }
        else
        {
            armazem.Codigo = armazem.Codigo.Trim();
        }

        // Limpar campos removidos
        // Campos removidos: Localidade, Telefone, ResponsavelNome, ResponsavelTelefone

        _db.ArmazensCatalogo.Add(armazem);
        await _db.SaveChangesAsync();

        return Ok(armazem);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateArmazem(int id, [FromBody] Armazem updated)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo.FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        // Validar unicidade do código se foi alterado
        if (armazem.Codigo != updated.Codigo.Trim() &&
            await _db.ArmazensCatalogo.AnyAsync(a => a.Codigo == updated.Codigo.Trim() && a.CriadoPor == uid && a.Id != id))
            return Conflict(new { message = "Já existe outro armazém com este código." });

        // Validar unicidade da localização se foi alterada
        if (armazem.Localizacao != updated.Localizacao.Trim() &&
            await _db.ArmazensCatalogo.AnyAsync(a => a.Localizacao == updated.Localizacao.Trim() && a.CriadoPor == uid && a.Id != id))
            return Conflict(new { message = "Já existe outro armazém com esta localização." });

        armazem.Codigo = updated.Codigo.Trim();
        armazem.Localizacao = updated.Localizacao.Trim();
        armazem.Nome = updated.Nome.Trim();
        armazem.Tipo = updated.Tipo;
        armazem.Morada = updated.Morada?.Trim();
        armazem.CodigoPostal = updated.CodigoPostal?.Trim();
        armazem.Pais = updated.Pais?.Trim();
        armazem.Email = updated.Email?.Trim();
        armazem.Observacoes = updated.Observacoes?.Trim();
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;

        // Limpar campos removidos
        // Campos removidos: Localidade, Telefone, ResponsavelNome, ResponsavelTelefone

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