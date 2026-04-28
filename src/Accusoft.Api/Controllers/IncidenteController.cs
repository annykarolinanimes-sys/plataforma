using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/incidentes")]
[Authorize]
public class IncidenteController : ControllerBase
{
    private readonly AppDbContext _db;

    public IncidenteController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/incidentes ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetIncidentes(
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] string? gravidade,
        [FromQuery] int? viagemId,
        [FromQuery] int? veiculoId,
        [FromQuery] int? clienteId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.Incidentes!
            .AsNoTracking()
            .Include(i => i.Viagem)
            .Include(i => i.Veiculo)
            .Include(i => i.Cliente)
            .Include(i => i.Atribuicao)
            .Where(i => i.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(i => i.Tipo == tipo);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(gravidade))
            query = query.Where(i => i.Gravidade == gravidade);

        if (viagemId.HasValue)
            query = query.Where(i => i.ViagemId == viagemId.Value);

        if (veiculoId.HasValue)
            query = query.Where(i => i.VeiculoId == veiculoId.Value);

        if (clienteId.HasValue)
            query = query.Where(i => i.ClienteId == clienteId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i =>
                i.NumeroIncidente.ToLower().Contains(s) ||
                i.Titulo.ToLower().Contains(s) ||
                (i.Descricao != null && i.Descricao.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.DataOcorrencia)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<IncidenteResponseDto>
        {
            Items = items.Select(MapToResponseDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/incidentes/{id} ─────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetIncidente(int id)
    {
        var uid = User.GetUserId();
        var incidente = await _db.Incidentes!
            .AsNoTracking()
            .Include(i => i.Viagem)
            .Include(i => i.Veiculo)
            .Include(i => i.Cliente)
            .Include(i => i.Atribuicao)
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        return Ok(MapToResponseDto(incidente));
    }

    // ─── GET /api/user/incidentes/por-viagem/{viagemId} ────────────────────────
    [HttpGet("por-viagem/{viagemId:int}")]
    public async Task<IActionResult> GetIncidentesPorViagem(int viagemId)
    {
        var uid = User.GetUserId();
        var incidentes = await _db.Incidentes!
            .AsNoTracking()
            .Include(i => i.Viagem)
            .Where(i => i.ViagemId == viagemId && i.UsuarioId == uid)
            .OrderByDescending(i => i.DataOcorrencia)
            .ToListAsync();

        return Ok(incidentes.Select(MapToResponseDto));
    }

    // ─── POST /api/user/incidentes ────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateIncidente([FromBody] IncidenteCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        var incidente = new Incidente
        {
            NumeroIncidente = GerarNumeroIncidente(),
            DataOcorrencia = dto.DataOcorrencia ?? DateTime.UtcNow,
            Tipo = dto.Tipo,
            Gravidade = dto.Gravidade,
            Status = "Aberto",
            Titulo = dto.Titulo.Trim(),
            Descricao = dto.Descricao?.Trim(),
            ViagemId = dto.ViagemId,
            VeiculoId = dto.VeiculoId,
            ClienteId = dto.ClienteId,
            AtribuicaoId = dto.AtribuicaoId,
            Causa = dto.Causa?.Trim(),
            AcaoCorretiva = dto.AcaoCorretiva?.Trim(),
            ResponsavelResolucao = dto.ResponsavelResolucao?.Trim(),
            CustoAssociado = dto.CustoAssociado,
            Observacoes = dto.Observacoes?.Trim(),
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.Incidentes!.Add(incidente);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetIncidente), new { id = incidente.Id }, MapToResponseDto(incidente));
    }

    // ─── PUT /api/user/incidentes/{id} ─────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateIncidente(int id, [FromBody] IncidenteUpdateDto dto)
    {
        var uid = User.GetUserId();
        var incidente = await _db.Incidentes!
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Status))
            incidente.Status = dto.Status;

        if (!string.IsNullOrWhiteSpace(dto.Gravidade))
            incidente.Gravidade = dto.Gravidade;

        if (!string.IsNullOrWhiteSpace(dto.Descricao))
            incidente.Descricao = dto.Descricao;

        if (!string.IsNullOrWhiteSpace(dto.Causa))
            incidente.Causa = dto.Causa;

        if (!string.IsNullOrWhiteSpace(dto.AcaoCorretiva))
            incidente.AcaoCorretiva = dto.AcaoCorretiva;

        if (!string.IsNullOrWhiteSpace(dto.ResponsavelResolucao))
            incidente.ResponsavelResolucao = dto.ResponsavelResolucao;

        if (dto.CustoAssociado.HasValue)
            incidente.CustoAssociado = dto.CustoAssociado.Value;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            incidente.Observacoes = dto.Observacoes;

        if (dto.DataResolucao.HasValue)
            incidente.DataResolucao = dto.DataResolucao.Value;

        incidente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(incidente));
    }

    // ─── POST /api/user/incidentes/{id}/resolver ───────────────────────────────
    [HttpPost("{id:int}/resolver")]
    public async Task<IActionResult> ResolverIncidente(int id, [FromBody] ResolverIncidenteDto dto)
    {
        var uid = User.GetUserId();
        var incidente = await _db.Incidentes!
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (incidente.Status == "Resolvido" || incidente.Status == "Fechado")
            return BadRequest(new { message = "Incidente já foi resolvido." });

        incidente.Status = "Resolvido";
        incidente.DataResolucao = DateTime.UtcNow;
        incidente.AcaoCorretiva = dto.AcaoCorretiva.Trim();
        incidente.ResponsavelResolucao = dto.ResponsavelResolucao?.Trim();
        incidente.CustoAssociado = dto.CustoAssociado;
        incidente.Observacoes = dto.Observacoes?.Trim();
        incidente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Incidente resolvido com sucesso.", incidenteId = incidente.Id });
    }

    // ─── POST /api/user/incidentes/{id}/fechar ─────────────────────────────────
    [HttpPost("{id:int}/fechar")]
    public async Task<IActionResult> FecharIncidente(int id)
    {
        var uid = User.GetUserId();
        var incidente = await _db.Incidentes!
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (incidente.Status != "Resolvido")
            return BadRequest(new { message = "Apenas incidentes resolvidos podem ser fechados." });

        incidente.Status = "Fechado";
        incidente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Incidente fechado com sucesso." });
    }

    // ─── DELETE /api/user/incidentes/{id} ──────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteIncidente(int id)
    {
        var uid = User.GetUserId();
        var incidente = await _db.Incidentes!
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        _db.Incidentes!.Remove(incidente);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Incidente removido com sucesso." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroIncidente()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultimo = _db.Incidentes!
            .Where(i => i.NumeroIncidente.StartsWith($"INC/{ano}/{mes}/"))
            .OrderByDescending(i => i.NumeroIncidente)
            .FirstOrDefault();

        if (ultimo is null)
            return $"INC/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultimo.NumeroIncidente.Split('/').Last());
        return $"INC/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private static IncidenteResponseDto MapToResponseDto(Incidente i)
    {
        return new IncidenteResponseDto
        {
            Id = i.Id,
            NumeroIncidente = i.NumeroIncidente,
            DataOcorrencia = i.DataOcorrencia,
            Tipo = i.Tipo,
            Gravidade = i.Gravidade,
            Status = i.Status,
            Titulo = i.Titulo,
            Descricao = i.Descricao,
            ViagemId = i.ViagemId,
            ViagemNumero = i.Viagem?.NumeroViagem,
            VeiculoId = i.VeiculoId,
            VeiculoMatricula = i.Veiculo?.Matricula,
            ClienteId = i.ClienteId,
            ClienteNome = i.Cliente?.Nome,
            AtribuicaoId = i.AtribuicaoId,
            AtribuicaoNumero = i.Atribuicao?.NumeroAtribuicao,
            DataResolucao = i.DataResolucao,
            Causa = i.Causa,
            AcaoCorretiva = i.AcaoCorretiva,
            ResponsavelResolucao = i.ResponsavelResolucao,
            CustoAssociado = i.CustoAssociado,
            Observacoes = i.Observacoes,
            CriadoEm = i.CriadoEm,
            AtualizadoEm = i.AtualizadoEm
        };
    }
}