using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;
public record VeiculoResponseDto(
    int     Id,
    string  Matricula,
    string  Marca,
    string  Modelo,
    string? Cor,
    int?    Ano,
    string? Vin,
    string? TipoCombustivel,
    int?    Cilindrada,
    int?    Potencia,
    int?    Lugares,
    decimal? Peso,
    int?    ProprietarioId,
    string? ProprietarioNome,
    string? ProprietarioCodigo,
    bool    Ativo,
    string? Observacoes,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm
);

public record VeiculoCreateDto(
    string  Matricula,
    string  Marca,
    string  Modelo,
    string? Cor,
    int?    Ano,
    string? Vin,
    string? TipoCombustivel,
    int?    Cilindrada,
    int?    Potencia,
    int?    Lugares,
    decimal? Peso,
    int?    ProprietarioId,
    string? Observacoes
);

public record VeiculoUpdateDto(
    string  Matricula,
    string  Marca,
    string  Modelo,
    string? Cor,
    int?    Ano,
    string? Vin,
    string? TipoCombustivel,
    int?    Cilindrada,
    int?    Potencia,
    int?    Lugares,
    decimal? Peso,
    int?    ProprietarioId,
    string? Observacoes,
    bool    Ativo
);

[ApiController]
[Route("api/user/veiculos")]
[Authorize]
public class VeiculosController : ControllerBase
{
    private readonly AppDbContext _db;

    public VeiculosController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetVeiculos(
        [FromQuery] string? search,
        [FromQuery] string? combustivel,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 15,
        [FromQuery] string  orderBy   = "marca",
        [FromQuery] string  orderDir  = "asc")
    {
        var uid = User.GetUserId();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.Veiculos
            .AsNoTracking()
            .Include(v => v.Proprietario)
            .Where(v => v.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                v.Matricula.ToLower().Contains(s) ||
                v.Marca.ToLower().Contains(s)     ||
                v.Modelo.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(combustivel))
            query = query.Where(v =>
                v.TipoCombustivel != null &&
                v.TipoCombustivel.ToLower() == combustivel.ToLower());

        if (ativo.HasValue)
            query = query.Where(v => v.Ativo == ativo.Value);

        // ── Ordenação dinâmica ────────────────────────────────────────────
        var descending = orderDir.ToLower() == "desc";
        query = orderBy.ToLower() switch
        {
            "matricula" => descending ? query.OrderByDescending(v => v.Matricula) : query.OrderBy(v => v.Matricula),
            "modelo"    => descending ? query.OrderByDescending(v => v.Modelo)    : query.OrderBy(v => v.Modelo),
            "ano"       => descending ? query.OrderByDescending(v => v.Ano)       : query.OrderBy(v => v.Ano),
            _           => descending ? query.OrderByDescending(v => v.Marca)     : query.OrderBy(v => v.Marca),
        };

        // ── Paginação ─────────────────────────────────────────────────────
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<VeiculoResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    // ── GET /api/user/veiculos/{id} ───────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetVeiculo(int id)
    {
        var uid = User.GetUserId();
        var v   = await _db.Veiculos
            .AsNoTracking()
            .Include(v => v.Proprietario)
            .FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        return v is null
            ? NotFound(new { message = "Veículo não encontrado." })
            : Ok(MapToDto(v));
    }

    // ── POST /api/user/veiculos ───────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateVeiculo([FromBody] VeiculoCreateDto dto)
    {
        var uid = User.GetUserId();

        // Validações de negócio
        if (string.IsNullOrWhiteSpace(dto.Matricula))
            return BadRequest(new { message = "Matrícula é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        // Unicidade de matrícula por utilizador
        var matriculaNorm = dto.Matricula.Trim().ToUpperInvariant();
        if (await _db.Veiculos.AnyAsync(v => v.Matricula == matriculaNorm && v.CriadoPor == uid))
            return Conflict(new { message = $"Já existe um veículo com a matrícula '{matriculaNorm}'." });

        var now = DateTimeOffset.UtcNow;
        var veiculo = new Veiculo
        {
            Matricula        = matriculaNorm,
            Marca            = dto.Marca.Trim(),
            Modelo           = dto.Modelo.Trim(),
            Cor              = dto.Cor?.Trim(),
            Ano              = dto.Ano,
            Vin              = dto.Vin?.Trim().ToUpperInvariant(),
            TipoCombustivel  = dto.TipoCombustivel?.Trim(),
            Cilindrada       = dto.Cilindrada,
            Potencia         = dto.Potencia,
            Lugares          = dto.Lugares,
            Peso             = dto.Peso,
            ProprietarioId   = dto.ProprietarioId,
            Observacoes      = dto.Observacoes?.Trim(),
            Ativo            = true,
            CriadoPor        = uid,
            CriadoEm         = now,
            AtualizadoEm     = now,
        };

        _db.Veiculos.Add(veiculo);
        await _db.SaveChangesAsync();

        // Recarrega com Proprietario incluído para o DTO
        await _db.Entry(veiculo).Reference(v => v.Proprietario).LoadAsync();

        return CreatedAtAction(nameof(GetVeiculo), new { id = veiculo.Id }, MapToDto(veiculo));
    }

    // ── PUT /api/user/veiculos/{id} ───────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateVeiculo(int id, [FromBody] VeiculoUpdateDto dto)
    {
        var uid     = User.GetUserId();
        var veiculo = await _db.Veiculos
            .Include(v => v.Proprietario)
            .FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        if (string.IsNullOrWhiteSpace(dto.Matricula))
            return BadRequest(new { message = "Matrícula é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        var matriculaNorm = dto.Matricula.Trim().ToUpperInvariant();

        // Unicidade: ignora o próprio registo
        if (veiculo.Matricula != matriculaNorm &&
            await _db.Veiculos.AnyAsync(v =>
                v.Matricula == matriculaNorm && v.CriadoPor == uid && v.Id != id))
            return Conflict(new { message = $"Já existe outro veículo com a matrícula '{matriculaNorm}'." });

        veiculo.Matricula       = matriculaNorm;
        veiculo.Marca           = dto.Marca.Trim();
        veiculo.Modelo          = dto.Modelo.Trim();
        veiculo.Cor             = dto.Cor?.Trim();
        veiculo.Ano             = dto.Ano;
        veiculo.Vin             = dto.Vin?.Trim().ToUpperInvariant();
        veiculo.TipoCombustivel = dto.TipoCombustivel?.Trim();
        veiculo.Cilindrada      = dto.Cilindrada;
        veiculo.Potencia        = dto.Potencia;
        veiculo.Lugares         = dto.Lugares;
        veiculo.Peso            = dto.Peso;
        veiculo.ProprietarioId  = dto.ProprietarioId;
        veiculo.Observacoes     = dto.Observacoes?.Trim();
        veiculo.Ativo           = dto.Ativo;
        veiculo.AtualizadoEm    = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        await _db.Entry(veiculo).Reference(v => v.Proprietario).LoadAsync();

        return Ok(MapToDto(veiculo));
    }

    // ── DELETE /api/user/veiculos/{id}  (soft-delete) ────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVeiculo(int id)
    {
        var uid     = User.GetUserId();
        var veiculo = await _db.Veiculos
            .FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo        = false;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo desativado com sucesso." });
    }

    // ── POST /api/user/veiculos/{id}/ativar ───────────────────────────────
    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarVeiculo(int id)
    {
        var uid     = User.GetUserId();
        var veiculo = await _db.Veiculos
            .FirstOrDefaultAsync(v => v.Id == id && v.CriadoPor == uid);

        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo        = true;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo ativado com sucesso." });
    }

    // ── Mapper ────────────────────────────────────────────────────────────
    private static VeiculoResponseDto MapToDto(Veiculo v) => new(
        v.Id, v.Matricula, v.Marca, v.Modelo,
        v.Cor, v.Ano, v.Vin, v.TipoCombustivel,
        v.Cilindrada, v.Potencia, v.Lugares, v.Peso,
        v.ProprietarioId,
        v.Proprietario?.Nome,
        v.Proprietario?.Codigo,
        v.Ativo, v.Observacoes,
        v.CriadoEm, v.AtualizadoEm
    );
}
