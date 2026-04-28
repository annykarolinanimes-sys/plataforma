using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/fechos-viagem")]
[Authorize]
public class FechoViagemController : ControllerBase
{
    private readonly AppDbContext _db;

    public FechoViagemController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/fechos-viagem ───────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetFechos(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        // ⚠️ CRUCIAL: NÃO usar Include com propriedade que não é navegação!
        var query = _db.FechosViagem!
            .AsNoTracking()
            .Where(f => f.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(f => f.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(f =>
                f.NumeroFecho.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.DataFecho)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Buscar atribuições separadamente (NÃO no Include)
        var atribuicaoIds = items.Select(f => f.AtribuicaoId).Distinct().ToList();
        var atribuicoes = new Dictionary<int, Atribuicao>();
        
        if (atribuicaoIds.Any())
        {
            var atribuicoesList = await _db.Atribuicoes!
                .AsNoTracking()
                .Where(a => atribuicaoIds.Contains(a.Id))
                .ToListAsync();
            atribuicoes = atribuicoesList.ToDictionary(a => a.Id, a => a);
        }

        var result = new PagedResult<FechoViagemResponseDto>
        {
            Items = items.Select(f => MapToResponseDto(f, atribuicoes.GetValueOrDefault(f.AtribuicaoId))).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/fechos-viagem/{id} ──────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFecho(int id)
    {
        var uid = User.GetUserId();
        var fecho = await _db.FechosViagem!
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fecho is null)
            return NotFound(new { message = "Fecho de viagem não encontrado." });

        var atribuicao = await _db.Atribuicoes!
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == fecho.AtribuicaoId);

        return Ok(MapToResponseDto(fecho, atribuicao));
    }

    // ─── GET /api/user/fechos-viagem/por-atribuicao/{atribuicaoId} ─────────────
    [HttpGet("por-atribuicao/{atribuicaoId:int}")]
    public async Task<IActionResult> GetFechoPorAtribuicao(int atribuicaoId)
    {
        var uid = User.GetUserId();
        var fecho = await _db.FechosViagem!
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.AtribuicaoId == atribuicaoId && f.UsuarioId == uid);

        if (fecho is null)
            return NotFound(new { message = "Nenhum fecho encontrado para esta atribuição." });

        var atribuicao = await _db.Atribuicoes!
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == fecho.AtribuicaoId);

        return Ok(MapToResponseDto(fecho, atribuicao));
    }

    // ─── POST /api/user/fechos-viagem ──────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateFecho([FromBody] FechoViagemCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        var atribuicao = await _db.Atribuicoes!
            .FirstOrDefaultAsync(a => a.Id == dto.AtribuicaoId && a.UsuarioId == uid);

        if (atribuicao is null)
            return BadRequest(new { message = "Atribuição não encontrada." });

        var existeFecho = await _db.FechosViagem!
            .AnyAsync(f => f.AtribuicaoId == dto.AtribuicaoId);
        
        if (existeFecho)
            return Conflict(new { message = "Já existe um fecho para esta atribuição." });

        var kmPercorridos = dto.QuilometrosInicio.HasValue && dto.QuilometrosFim.HasValue
            ? dto.QuilometrosFim.Value - dto.QuilometrosInicio.Value
            : (int?)null;

        var custoTotal = (dto.CombustivelCusto ?? 0) + 
                         (dto.PortagensCusto ?? 0) + 
                         (dto.OutrosCustos ?? 0);

        string? entregasNaoRealizadasJson = null;
        if (dto.EntregasNaoRealizadasIds != null && dto.EntregasNaoRealizadasIds.Any())
        {
            entregasNaoRealizadasJson = JsonSerializer.Serialize(dto.EntregasNaoRealizadasIds);
        }

        var fecho = new FechoViagem
        {
            NumeroFecho = GerarNumeroFecho(),
            AtribuicaoId = dto.AtribuicaoId,
            DataFecho = DateTime.UtcNow,
            Status = "Pendente",
            DataInicioReal = dto.DataInicioReal,
            DataFimReal = dto.DataFimReal,
            CombustivelLitros = dto.CombustivelLitros,
            CombustivelCusto = dto.CombustivelCusto,
            PortagensCusto = dto.PortagensCusto,
            OutrosCustos = dto.OutrosCustos,
            CustosExtrasDescricao = dto.CustosExtrasDescricao,
            CustoTotal = custoTotal,
            QuilometrosInicio = dto.QuilometrosInicio,
            QuilometrosFim = dto.QuilometrosFim,
            QuilometrosPercorridos = kmPercorridos,
            EntregasNaoRealizadasIds = entregasNaoRealizadasJson,
            EntregasPendentesObs = dto.EntregasPendentesObs,
            TemIncidentes = dto.TemIncidentes,
            IncidentesDescricao = dto.IncidentesDescricao,
            Observacoes = dto.Observacoes,
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.FechosViagem!.Add(fecho);
        await _db.SaveChangesAsync();

        atribuicao.Status = "Concluida";
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFecho), new { id = fecho.Id }, MapToResponseDto(fecho, atribuicao));
    }

    // ─── PUT /api/user/fechos-viagem/{id} ──────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFecho(int id, [FromBody] FechoViagemUpdateDto dto)
    {
        var uid = User.GetUserId();
        var fecho = await _db.FechosViagem!
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fecho is null)
            return NotFound(new { message = "Fecho não encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Status))
            fecho.Status = dto.Status;

        if (dto.DataInicioReal.HasValue)
            fecho.DataInicioReal = dto.DataInicioReal.Value;

        if (dto.DataFimReal.HasValue)
            fecho.DataFimReal = dto.DataFimReal.Value;

        if (dto.CombustivelLitros.HasValue)
            fecho.CombustivelLitros = dto.CombustivelLitros.Value;

        if (dto.CombustivelCusto.HasValue)
            fecho.CombustivelCusto = dto.CombustivelCusto.Value;

        if (dto.PortagensCusto.HasValue)
            fecho.PortagensCusto = dto.PortagensCusto.Value;

        if (dto.OutrosCustos.HasValue)
            fecho.OutrosCustos = dto.OutrosCustos.Value;

        if (!string.IsNullOrWhiteSpace(dto.CustosExtrasDescricao))
            fecho.CustosExtrasDescricao = dto.CustosExtrasDescricao;

        if (dto.QuilometrosInicio.HasValue)
            fecho.QuilometrosInicio = dto.QuilometrosInicio.Value;

        if (dto.QuilometrosFim.HasValue)
            fecho.QuilometrosFim = dto.QuilometrosFim.Value;

        if (dto.QuilometrosInicio.HasValue && dto.QuilometrosFim.HasValue)
            fecho.QuilometrosPercorridos = dto.QuilometrosFim.Value - dto.QuilometrosInicio.Value;

        if (dto.EntregasNaoRealizadasIds != null)
            fecho.EntregasNaoRealizadasIds = JsonSerializer.Serialize(dto.EntregasNaoRealizadasIds);

        if (!string.IsNullOrWhiteSpace(dto.EntregasPendentesObs))
            fecho.EntregasPendentesObs = dto.EntregasPendentesObs;

        if (dto.TemIncidentes.HasValue)
            fecho.TemIncidentes = dto.TemIncidentes.Value;

        if (!string.IsNullOrWhiteSpace(dto.IncidentesDescricao))
            fecho.IncidentesDescricao = dto.IncidentesDescricao;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            fecho.Observacoes = dto.Observacoes;

        if (dto.Faturado.HasValue)
            fecho.Faturado = dto.Faturado.Value;

        fecho.CustoTotal = (fecho.CombustivelCusto ?? 0) + 
                           (fecho.PortagensCusto ?? 0) + 
                           (fecho.OutrosCustos ?? 0);

        fecho.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var atribuicao = await _db.Atribuicoes!.FirstOrDefaultAsync(a => a.Id == fecho.AtribuicaoId);
        return Ok(MapToResponseDto(fecho, atribuicao));
    }

    // ─── POST /api/user/fechos-viagem/{id}/processar ───────────────────────────
    [HttpPost("{id:int}/processar")]
    public async Task<IActionResult> ProcessarFecho(int id)
    {
        var uid = User.GetUserId();
        var fecho = await _db.FechosViagem!
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fecho is null)
            return NotFound(new { message = "Fecho não encontrado." });

        if (fecho.Status != "Pendente")
            return BadRequest(new { message = "Apenas fechos pendentes podem ser processados." });

        fecho.Status = "Processado";
        fecho.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Fecho processado com sucesso." });
    }

    // ─── DELETE /api/user/fechos-viagem/{id} ───────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFecho(int id)
    {
        var uid = User.GetUserId();
        var fecho = await _db.FechosViagem!
            .FirstOrDefaultAsync(f => f.Id == id && f.UsuarioId == uid);

        if (fecho is null)
            return NotFound(new { message = "Fecho não encontrado." });

        fecho.Status = "Cancelado";
        fecho.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Fecho cancelado com sucesso." });
    }

    private string GerarNumeroFecho()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultimo = _db.FechosViagem!
            .Where(f => f.NumeroFecho.StartsWith($"FECHO/{ano}/{mes}/"))
            .OrderByDescending(f => f.NumeroFecho)
            .FirstOrDefault();

        if (ultimo is null)
            return $"FECHO/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultimo.NumeroFecho.Split('/').Last());
        return $"FECHO/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private FechoViagemResponseDto MapToResponseDto(FechoViagem f, Atribuicao? atribuicao)
    {
        var tempoPlaneado = atribuicao?.DataPrevistaFim.HasValue == true && atribuicao?.DataPrevistaInicio.HasValue == true
            ? atribuicao.DataPrevistaFim.Value - atribuicao.DataPrevistaInicio.Value
            : (TimeSpan?)null;

        var tempoReal = f.DataInicioReal.HasValue && f.DataFimReal.HasValue
            ? f.DataFimReal.Value - f.DataInicioReal.Value
            : (TimeSpan?)null;

        var diferencaTempo = tempoPlaneado.HasValue && tempoReal.HasValue
            ? tempoReal.Value - tempoPlaneado.Value
            : (TimeSpan?)null;

        int totalEntregas = atribuicao?.Entregas?.Count ?? 0;
        var entregasNaoRealizadasIds = string.IsNullOrEmpty(f.EntregasNaoRealizadasIds)
            ? new List<int>()
            : JsonSerializer.Deserialize<List<int>>(f.EntregasNaoRealizadasIds) ?? new List<int>();

        int entregasNaoRealizadas = entregasNaoRealizadasIds.Count;
        int entregasRealizadas = totalEntregas - entregasNaoRealizadas;

        return new FechoViagemResponseDto
        {
            Id = f.Id,
            NumeroFecho = f.NumeroFecho,
            AtribuicaoId = f.AtribuicaoId,
            AtribuicaoNumero = atribuicao?.NumeroAtribuicao,
            ClienteNome = atribuicao?.ClienteNome,
            DataFecho = f.DataFecho,
            Status = f.Status,
            DataInicioReal = f.DataInicioReal,
            DataFimReal = f.DataFimReal,
            TempoTotalReal = tempoReal,
            TempoPlaneado = tempoPlaneado,
            DiferencaTempo = diferencaTempo,
            CombustivelLitros = f.CombustivelLitros,
            CombustivelCusto = f.CombustivelCusto,
            PortagensCusto = f.PortagensCusto,
            OutrosCustos = f.OutrosCustos,
            CustosExtrasDescricao = f.CustosExtrasDescricao,
            CustoTotal = f.CustoTotal,
            QuilometrosInicio = f.QuilometrosInicio,
            QuilometrosFim = f.QuilometrosFim,
            QuilometrosPercorridos = f.QuilometrosPercorridos,
            TotalEntregas = totalEntregas,
            EntregasRealizadas = entregasRealizadas,
            EntregasNaoRealizadas = entregasNaoRealizadas,
            EntregasPendentesObs = f.EntregasPendentesObs,
            TemIncidentes = f.TemIncidentes,
            IncidentesDescricao = f.IncidentesDescricao,
            Faturado = f.Faturado,
            Observacoes = f.Observacoes,
            CriadoEm = f.CriadoEm,
            AtualizadoEm = f.AtualizadoEm
        };
    }
}