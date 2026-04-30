using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/veiculos")]
[Authorize]
public class VeiculosController : ControllerBase
{
    private readonly AppDbContext _db;

    public VeiculosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetVeiculos(
        [FromQuery] string? search,
        [FromQuery] bool? ativo)
    {
        var uid = User.GetUserId();
        var query = _db.Veiculos
            .AsNoTracking()
            .Include(v => v.Proprietario)
            .Where(v => v.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v =>
                v.Matricula.ToLower().Contains(search.ToLower()) ||
                v.Marca.ToLower().Contains(search.ToLower()) ||
                v.Modelo.ToLower().Contains(search.ToLower()));

        if (ativo.HasValue)
            query = query.Where(v => v.Ativo == ativo.Value);

        var veiculos = await query.OrderBy(v => v.Marca).ThenBy(v => v.Modelo).ToListAsync();
        return Ok(veiculos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetVeiculo(int id)
    {
        var veiculo = await _db.Veiculos
            .Include(v => v.Proprietario)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        return Ok(veiculo);
    }

    [HttpPost]
    public async Task<IActionResult> CreateVeiculo([FromBody] Veiculo veiculo)
    {
        var uid = User.GetUserId();

        if (string.IsNullOrWhiteSpace(veiculo.Matricula))
            return BadRequest(new { message = "Matrícula é obrigatória." });
        if (string.IsNullOrWhiteSpace(veiculo.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(veiculo.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        if (await _db.Veiculos.AnyAsync(v => v.Matricula == veiculo.Matricula && v.CriadoPor == uid))
            return Conflict(new { message = "Já existe um veículo com esta matrícula." });

        veiculo.CriadoPor = uid;
        veiculo.CriadoEm = DateTimeOffset.UtcNow;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;

        _db.Veiculos.Add(veiculo);
        await _db.SaveChangesAsync();

        return Ok(veiculo);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateVeiculo(int id, [FromBody] Veiculo updated)
    {
        var uid = User.GetUserId();
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        if (string.IsNullOrWhiteSpace(updated.Matricula))
            return BadRequest(new { message = "Matrícula é obrigatória." });
        if (string.IsNullOrWhiteSpace(updated.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(updated.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        if (veiculo.Matricula != updated.Matricula &&
            await _db.Veiculos.AnyAsync(v => v.Matricula == updated.Matricula && v.CriadoPor == uid && v.Id != id))
            return Conflict(new { message = "Já existe outro veículo com esta matrícula." });

        veiculo.Matricula = updated.Matricula;
        veiculo.Marca = updated.Marca;
        veiculo.Modelo = updated.Modelo;
        veiculo.Cor = updated.Cor;
        veiculo.Ano = updated.Ano;
        veiculo.Vin = updated.Vin;
        veiculo.TipoCombustivel = updated.TipoCombustivel;
        veiculo.Cilindrada = updated.Cilindrada;
        veiculo.Potencia = updated.Potencia;
        veiculo.Lugares = updated.Lugares;
        veiculo.Peso = updated.Peso;
        veiculo.ProprietarioId = updated.ProprietarioId;
        veiculo.Observacoes = updated.Observacoes;
        veiculo.Ativo = updated.Ativo;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(veiculo);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVeiculo(int id)
    {
        var uid = User.GetUserId();
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo = false;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarVeiculo(int id)
    {
        var uid = User.GetUserId();
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo = true;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo ativado com sucesso." });
    }
}