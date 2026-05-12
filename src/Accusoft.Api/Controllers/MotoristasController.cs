using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/motoristas")]
[Authorize]
public class MotoristasController : ControllerBase
{
    private readonly AppDbContext _db;

    public MotoristasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMotoristas(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string orderBy = "nome",
        [FromQuery] string orderDir = "asc")
    {
        var uid = User.GetUserId();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _db.Motoristas
            .AsNoTracking()
            .Where(m => m.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(m =>
                m.Nome.ToLower().Contains(s) ||
                m.Telefone.ToLower().Contains(s) ||
                m.CartaConducao.ToLower().Contains(s));
        }

        if (ativo.HasValue)
            query = query.Where(m => m.Ativo == ativo.Value);

        var desc = orderDir.ToLower() == "desc";
        query = orderBy.ToLower() switch
        {
            "telefone" => desc ? query.OrderByDescending(m => m.Telefone) : query.OrderBy(m => m.Telefone),
            "carta_conducao" => desc ? query.OrderByDescending(m => m.CartaConducao) : query.OrderBy(m => m.CartaConducao),
            _ => desc ? query.OrderByDescending(m => m.Nome) : query.OrderBy(m => m.Nome),
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<MotoristaResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMotorista(int id)
    {
        var uid = User.GetUserId();
        var m = await _db.Motoristas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.CriadoPor == uid);

        return m is null
            ? NotFound(new { message = "Motorista não encontrado." })
            : Ok(MapToDto(m));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMotorista([FromBody] MotoristaCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { message = "Erro de validação", errors });
        }

        var uid = User.GetUserId();

        // Validar se a transportadora existe
        var transportadora = await _db.TransportadorasCatalogo
            .FirstOrDefaultAsync(t => t.Codigo == dto.TransportadoraId && t.CriadoPor == uid);

        if (transportadora is null)
            return BadRequest(new { message = "Transportadora não encontrada." });

        var now = DateTimeOffset.UtcNow;

        var motorista = new Motorista
        {
            Nome = dto.Nome.Trim(),
            Telefone = dto.Telefone.Trim(),
            CartaConducao = dto.CartaConducao.Trim().ToUpper(),
            TransportadoraId = dto.TransportadoraId,
            Ativo = true,
            CriadoPor = uid,
            CriadoEm = now,
            AtualizadoEm = now,
        };

        try
        {
            _db.Motoristas.Add(motorista);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Erro ao salvar: {ex.Message}" });
        }

        return CreatedAtAction(nameof(GetMotorista), new { id = motorista.Id }, MapToDto(motorista));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMotorista(int id, [FromBody] MotoristaUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        // Validar se a transportadora existe (se foi fornecida)
        if (!string.IsNullOrWhiteSpace(dto.TransportadoraId))
        {
            var transportadora = await _db.TransportadorasCatalogo
                .FirstOrDefaultAsync(t => t.Codigo == dto.TransportadoraId && t.CriadoPor == uid);

            if (transportadora is null)
                return BadRequest(new { message = "Transportadora não encontrada." });
        }

        var motorista = await _db.Motoristas
            .FirstOrDefaultAsync(m => m.Id == id && m.CriadoPor == uid);

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Nome = dto.Nome.Trim();
        motorista.Telefone = dto.Telefone.Trim();
        motorista.CartaConducao = dto.CartaConducao.Trim().ToUpper();
        if (!string.IsNullOrWhiteSpace(dto.TransportadoraId))
            motorista.TransportadoraId = dto.TransportadoraId.Trim().ToUpper();
        motorista.Ativo = dto.Ativo;
        motorista.AtualizadoEm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(motorista));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMotorista(int id)
    {
        var uid = User.GetUserId();
        var motorista = await _db.Motoristas
            .FirstOrDefaultAsync(m => m.Id == id && m.CriadoPor == uid);

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Ativo = false;
        motorista.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Motorista desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarMotorista(int id)
    {
        var uid = User.GetUserId();
        var motorista = await _db.Motoristas
            .FirstOrDefaultAsync(m => m.Id == id && m.CriadoPor == uid);

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Ativo = true;
        motorista.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Motorista ativado com sucesso." });
    }

    private static MotoristaResponseDto MapToDto(Motorista m) => new(
        Id: m.Id,
        Nome: m.Nome,
        Telefone: m.Telefone,
        CartaConducao: m.CartaConducao,
        TransportadoraId: m.TransportadoraId,
        Ativo: m.Ativo,
        CriadoEm: m.CriadoEm,
        AtualizadoEm: m.AtualizadoEm
    );
}