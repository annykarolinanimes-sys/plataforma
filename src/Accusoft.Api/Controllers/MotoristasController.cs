using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/motoristas")]
[Authorize]
public class MotoristasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MotoristasController> _logger;

    public MotoristasController(AppDbContext db, ILogger<MotoristasController> logger)
    {
        _db = db;
        _logger = logger;
    }

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

        var query = _db.Motoristas.AsNoTracking();

        if (!User.IsAdmin())
            query = query.Where(m => m.CriadoPor == uid);

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
            "telefone"       => desc ? query.OrderByDescending(m => m.Telefone)       : query.OrderBy(m => m.Telefone),
            "carta_conducao" => desc ? query.OrderByDescending(m => m.CartaConducao)  : query.OrderBy(m => m.CartaConducao),
            _                => desc ? query.OrderByDescending(m => m.Nome)           : query.OrderBy(m => m.Nome),
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<MotoristaResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMotorista(int id)
    {
        var uid = User.GetUserId();
        var query = _db.Motoristas.AsNoTracking().Where(m => m.Id == id);

        if (!User.IsAdmin())
            query = query.Where(m => m.CriadoPor == uid);

        var m = await query.FirstOrDefaultAsync();

        return m is null
            ? NotFound(new { message = "Motorista não encontrado." })
            : Ok(MapToDto(m));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMotorista([FromBody] MotoristaCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { message = "Erro de validação.", errors });
        }

        var uid = User.GetUserId();
        var transportadora = await FindTransportadoraAsync(dto.TransportadoraId, uid);

        if (transportadora is null)
            return BadRequest(new { message = "Transportadora não encontrada." });

        var now = DateTimeOffset.UtcNow;

        var normalizedCarta = dto.CartaConducao.Trim().ToUpperInvariant();
        if (await _db.Motoristas.AnyAsync(m => m.CartaConducao == normalizedCarta))
            return Conflict(new { message = "Já existe um motorista com esta carta de condução." });

        if (!string.IsNullOrWhiteSpace(dto.Nif))
        {
            var normalizedNif = dto.Nif.Trim().ToLowerInvariant();
            if (await _db.ClientesCatalogo.AnyAsync(c =>
                    c.Contribuinte != null && c.Contribuinte.ToLower() == normalizedNif))
                return Conflict(new { message = "Já existe um cliente com este NIF." });

            if (await _db.FornecedoresCatalogo.AnyAsync(f =>
                    f.Nif != null && f.Nif.ToLower() == normalizedNif))
                return Conflict(new { message = "Já existe um fornecedor com este NIF." });
        }

        var motorista = new Motorista
        {
            Nome                  = dto.Nome.Trim(),
            Telefone              = dto.Telefone.Trim(),
            CartaConducao         = normalizedCarta,
            Nif                   = dto.Nif?.Trim(),
            ValidadeCartaConducao = dto.ValidadeCartaConducao,
            TransportadoraId      = transportadora.Codigo,
            Ativo                 = true,
            CriadoPor             = uid,
            CriadoEm              = now,
            AtualizadoEm          = now,
        };

        try
        {
            _db.Motoristas.Add(motorista);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating motorista for user {UserId}", uid);
            return StatusCode(500, new { message = "Erro ao guardar motorista." });
        }

        return CreatedAtAction(nameof(GetMotorista), new { id = motorista.Id }, MapToDto(motorista));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMotorista(int id, [FromBody] MotoristaUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        var motoristaQuery = _db.Motoristas.Where(m => m.Id == id);
        if (!User.IsAdmin())
            motoristaQuery = motoristaQuery.Where(m => m.CriadoPor == uid);

        var motorista = await motoristaQuery.FirstOrDefaultAsync();

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.TransportadoraId))
        {
            var transportadora = await FindTransportadoraAsync(dto.TransportadoraId, uid);
            if (transportadora is null)
                return BadRequest(new { message = "Transportadora não encontrada." });

            motorista.TransportadoraId = transportadora.Codigo;
        }

        var normalizedCarta = dto.CartaConducao.Trim().ToUpperInvariant();
        if (await _db.Motoristas.AnyAsync(m => m.Id != id && m.CartaConducao == normalizedCarta))
            return Conflict(new { message = "Já existe um motorista com esta carta de condução." });

        if (!string.IsNullOrWhiteSpace(dto.Nif))
        {
            var normalizedNif = dto.Nif.Trim().ToLowerInvariant();
            var currentNif = motorista.Nif?.Trim().ToLowerInvariant();

            if (currentNif != normalizedNif)
            {
                if (await _db.ClientesCatalogo.AnyAsync(c =>
                        c.Contribuinte != null && c.Contribuinte.ToLower() == normalizedNif))
                    return Conflict(new { message = "Já existe um cliente com este NIF." });

                if (await _db.FornecedoresCatalogo.AnyAsync(f =>
                        f.Nif != null && f.Nif.ToLower() == normalizedNif))
                    return Conflict(new { message = "Já existe um fornecedor com este NIF." });
            }
        }

        motorista.Nome                  = dto.Nome.Trim();
        motorista.Telefone              = dto.Telefone.Trim();
        motorista.CartaConducao         = normalizedCarta;
        motorista.Nif                   = dto.Nif?.Trim();
        motorista.ValidadeCartaConducao = dto.ValidadeCartaConducao;
        motorista.Ativo                 = dto.Ativo;
        motorista.AtualizadoEm          = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(motorista));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMotorista(int id)
    {
        var uid = User.GetUserId();
        var motoristaQuery = _db.Motoristas.Where(m => m.Id == id);

        if (!User.IsAdmin())
            motoristaQuery = motoristaQuery.Where(m => m.CriadoPor == uid);

        var motorista = await motoristaQuery.FirstOrDefaultAsync();

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Ativo        = false;
        motorista.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Motorista desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarMotorista(int id)
    {
        var uid = User.GetUserId();
        var motoristaQuery = _db.Motoristas.Where(m => m.Id == id);

        if (!User.IsAdmin())
            motoristaQuery = motoristaQuery.Where(m => m.CriadoPor == uid);

        var motorista = await motoristaQuery.FirstOrDefaultAsync();

        if (motorista is null)
            return NotFound(new { message = "Motorista não encontrado." });

        motorista.Ativo        = true;
        motorista.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Motorista ativado com sucesso." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MotoristaResponseDto MapToDto(Motorista m) => new(
        Id:                 m.Id,
        Nome:               m.Nome,
        Telefone:           m.Telefone,
        CartaConducao:      m.CartaConducao,
        Nif:                m.Nif,
        ValidadeCartaConducao: m.ValidadeCartaConducao,
        TransportadoraId:   m.TransportadoraId ?? string.Empty,
        Ativo:              m.Ativo,
        CriadoEm:           m.CriadoEm,
        AtualizadoEm:       m.AtualizadoEm
    );

    private async Task<TransportadoraCatalogo?> FindTransportadoraAsync(string? transportadoraId, int uid)
    {
        if (string.IsNullOrWhiteSpace(transportadoraId))
            return null;

        var trimmed    = transportadoraId.Trim();
        var normalized = trimmed.ToLowerInvariant();

        var query = _db.TransportadorasCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(t => t.CriadoPor == uid);

        // Try numeric ID first
        if (int.TryParse(trimmed, out var numId))
        {
            var byId = await query.FirstOrDefaultAsync(t => t.Id == numId);
            if (byId is not null) return byId;
        }

        // Try by code (case-insensitive)
        return await query.FirstOrDefaultAsync(t =>
            t.Codigo.ToLower() == normalized);
    }
}
