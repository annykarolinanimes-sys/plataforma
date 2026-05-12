using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/gestao-viagens")]
[Authorize]
public class GestaoViagemController : ControllerBase
{
    private readonly AppDbContext _db;

    public GestaoViagemController(AppDbContext db) => _db = db;

    private static readonly HashSet<string> StatusFinais = ["Concluida", "Cancelada"];

    [HttpGet]
    public async Task<IActionResult> GetViagens(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 15)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);
        var uid  = User.GetUserId();

        var query = _db.GestaoViagens
            .AsNoTracking()
            .Include(v => v.Veiculo)
            .Include(v => v.Motorista)
            .Include(v => v.Transportadora)
            .Include(v => v.Cliente)
            .Where(v => v.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(v => v.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                v.NumeroViagem.ToLower().Contains(s) ||
                (v.Veiculo   != null && v.Veiculo.Matricula.ToLower().Contains(s)) ||
                (v.Motorista != null && v.Motorista.Nome.ToLower().Contains(s)) ||
                (v.Cliente   != null && v.Cliente.Nome.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<GestaoViagemResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetViagem(int id)
    {
        var uid    = User.GetUserId();
        var viagem = await FindViagem(id, uid);

        return viagem is null
            ? NotFound(new { message = "Viagem não encontrada." })
            : Ok(MapToDto(viagem));
    }

    [HttpPost]
    public async Task<IActionResult> CreateViagem([FromBody] GestaoViagemCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Erro de validação.",
                errors  = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        // FIX: validação de datas server-side
        if (dto.DataInicioPlaneada.HasValue && dto.DataFimPlaneada.HasValue &&
            dto.DataFimPlaneada < dto.DataInicioPlaneada)
            return BadRequest(new { message = "A data de fim não pode ser anterior à data de início." });

        var uid    = User.GetUserId();
        var numero = await GerarNumeroViagem(uid);
        var now    = DateTimeOffset.UtcNow;

        var viagem = new GestaoViagem
        {
            NumeroViagem       = numero,
            Status             = "Planeada",
            Prioridade         = dto.Prioridade,
            DataCriacao        = DateTime.UtcNow,
            DataInicioPlaneada = EnsureUtc(dto.DataInicioPlaneada),
            DataFimPlaneada    = EnsureUtc(dto.DataFimPlaneada),
            VeiculoId          = dto.VeiculoId,
            MotoristaId        = dto.MotoristaId,
            TransportadoraId   = dto.TransportadoraId,
            ClienteId          = dto.ClienteId,
            Origem             = dto.Origem?.Trim(),
            Destino            = dto.Destino?.Trim(),
            PrecoPorKm         = dto.PrecoPorKm,
            CargaDescricao     = dto.CargaDescricao?.Trim(),
            CargaPeso          = dto.CargaPeso,
            CargaVolume        = dto.CargaVolume,
            CargaObservacoes   = dto.CargaObservacoes?.Trim(),
            DistanciaTotalKm   = dto.DistanciaTotalKm,
            TempoEstimadoHoras = dto.TempoEstimadoHoras,
            Observacoes        = dto.Observacoes?.Trim(),
            UsuarioId          = uid,
            CriadoEm           = now,
            AtualizadoEm       = now,
        };

        _db.GestaoViagens.Add(viagem);
        await _db.SaveChangesAsync();

        var criada = await FindViagem(viagem.Id, uid);
        return CreatedAtAction(nameof(GetViagem), new { id = viagem.Id }, MapToDto(criada!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateViagem(int id, [FromBody] GestaoViagemUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid    = User.GetUserId();
        var viagem = await FindViagem(id, uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (StatusFinais.Contains(viagem.Status))
            return BadRequest(new
            {
                message = $"Não é possível editar uma viagem com status '{viagem.Status}'."
            });

        // FIX: validação de datas
        var inicio = dto.DataInicioPlaneada ?? viagem.DataInicioPlaneada;
        var fim    = dto.DataFimPlaneada    ?? viagem.DataFimPlaneada;
        if (inicio.HasValue && fim.HasValue && fim < inicio)
            return BadRequest(new { message = "A data de fim não pode ser anterior à data de início." });

        // Campos de planeamento — só editáveis quando Planeada
        if (viagem.Status == "Planeada")
        {
            if (!string.IsNullOrWhiteSpace(dto.Status))       viagem.Status             = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.Prioridade))   viagem.Prioridade         = dto.Prioridade;
            if (dto.DataInicioPlaneada.HasValue) viagem.DataInicioPlaneada = EnsureUtc(dto.DataInicioPlaneada.Value);
            if (dto.DataFimPlaneada.HasValue)    viagem.DataFimPlaneada    = EnsureUtc(dto.DataFimPlaneada.Value);
            if (dto.VeiculoId.HasValue)          viagem.VeiculoId          = dto.VeiculoId.Value;
            if (dto.MotoristaId.HasValue)         viagem.MotoristaId        = dto.MotoristaId.Value;
            if (dto.TransportadoraId.HasValue)    viagem.TransportadoraId   = dto.TransportadoraId.Value;
            if (dto.ClienteId.HasValue)           viagem.ClienteId          = dto.ClienteId.Value;
            if (dto.Origem         is not null)   viagem.Origem             = dto.Origem.Trim();
            if (dto.Destino        is not null)   viagem.Destino            = dto.Destino.Trim();
            if (dto.PrecoPorKm.HasValue)          viagem.PrecoPorKm         = dto.PrecoPorKm.Value;
            if (dto.CargaDescricao   is not null) viagem.CargaDescricao     = dto.CargaDescricao.Trim();
            if (dto.CargaPeso.HasValue)            viagem.CargaPeso          = dto.CargaPeso.Value;
            if (dto.CargaVolume.HasValue)          viagem.CargaVolume        = dto.CargaVolume.Value;
            if (dto.CargaObservacoes is not null) viagem.CargaObservacoes   = dto.CargaObservacoes.Trim();
            if (dto.DistanciaTotalKm.HasValue)     viagem.DistanciaTotalKm   = dto.DistanciaTotalKm.Value;
            if (dto.TempoEstimadoHoras.HasValue)   viagem.TempoEstimadoHoras = dto.TempoEstimadoHoras.Value;
        }

        // Campos de execução real — editáveis apenas em EmCurso
        if (viagem.Status == "EmCurso")
        {
            if (dto.DataInicioReal.HasValue)         viagem.DataInicioReal         = EnsureUtc(dto.DataInicioReal.Value);
            if (dto.DataFimReal.HasValue)             viagem.DataFimReal             = EnsureUtc(dto.DataFimReal.Value);
            if (dto.DistanciaPercorridaKm.HasValue)  viagem.DistanciaPercorridaKm  = dto.DistanciaPercorridaKm.Value;
        }

        // Observações sempre editáveis
        if (dto.Observacoes is not null) viagem.Observacoes = dto.Observacoes.Trim();

        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var updated = await FindViagem(id, uid);
        return Ok(MapToDto(updated!));
    }

    private static DateTime? EnsureUtc(DateTime? date)
    {
        if (!date.HasValue)
            return null;

        var value = date.Value;
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
    }

    private static DateTime EnsureUtc(DateTime date)
    {
        if (date.Kind == DateTimeKind.Utc)
            return date;

        if (date.Kind == DateTimeKind.Local)
            return date.ToUniversalTime();

        return DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
    }

    // ── POST /api/user/gestao-viagens/{id}/iniciar ────────────────────────────
    [HttpPost("{id:int}/iniciar")]
    public async Task<IActionResult> IniciarViagem(int id)
    {
        var uid    = User.GetUserId();
        var viagem = await FindViagem(id, uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (viagem.Status != "Planeada")
            return BadRequest(new { message = "Apenas viagens Planeadas podem ser iniciadas." });

        viagem.Status          = "EmCurso";
        viagem.DataInicioReal  = DateTime.UtcNow;
        viagem.AtualizadoEm    = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Viagem iniciada com sucesso." });
    }

    // ── POST /api/user/gestao-viagens/{id}/concluir ───────────────────────────
    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirViagem(int id)
    {
        var uid    = User.GetUserId();
        var viagem = await FindViagem(id, uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (viagem.Status != "EmCurso")
            return BadRequest(new { message = "Apenas viagens Em Curso podem ser concluídas." });

        var agora  = DateTime.UtcNow;
        viagem.Status       = "Concluida";
        viagem.DataFimReal  = agora;
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;

        // Calcular tempo real em horas
        if (viagem.DataInicioReal.HasValue)
            viagem.TempoRealHoras = (decimal)(agora - viagem.DataInicioReal.Value).TotalHours;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Viagem concluída com sucesso." });
    }

    // ── DELETE /api/user/gestao-viagens/{id} (Soft Delete → Cancelada) ────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteViagem(int id)
    {
        var uid    = User.GetUserId();
        var viagem = await FindViagem(id, uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (StatusFinais.Contains(viagem.Status))
            return BadRequest(new
            {
                message = $"Não é possível cancelar uma viagem com status '{viagem.Status}'."
            });

        viagem.Status       = "Cancelada";
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Viagem cancelada com sucesso." });
    }

    // ── Métodos privados ──────────────────────────────────────────────────────

    private async Task<GestaoViagem?> FindViagem(int id, int uid)
    {
        return await _db.GestaoViagens
            .Include(v => v.Veiculo)
            .Include(v => v.Motorista)
            .Include(v => v.Transportadora)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);
    }

    private async Task<string> GerarNumeroViagem(int userId)
    {
        var agora   = DateTime.UtcNow;
        var prefixo = $"VGM-{agora:yyyyMM}-";

        var existentes = await _db.GestaoViagens
            .Where(v => v.UsuarioId == userId && v.NumeroViagem.StartsWith(prefixo))
            .Select(v => v.NumeroViagem)
            .ToListAsync();

        var maxSeq = existentes
            .Select(n => {
                var parts = n.Split('-');
                return parts.Length == 3 && int.TryParse(parts[2], out var s) ? s : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefixo}{(maxSeq + 1):D3}";
    }

    private static GestaoViagemResponseDto MapToDto(GestaoViagem v)
    {
        // FIX: AtrasoHoras calculado server-side com base nas datas reais vs planeadas
        decimal? atrasoHoras = null;
        if (v.DataFimReal.HasValue && v.DataFimPlaneada.HasValue &&
            v.DataFimReal.Value > v.DataFimPlaneada.Value)
        {
            atrasoHoras = (decimal)(v.DataFimReal.Value - v.DataFimPlaneada.Value).TotalHours;
        }

        // FIX: ProgressoPercentual baseado na distância percorrida
        decimal progresso = 0;
        if (v.DistanciaTotalKm > 0)
            progresso = Math.Min(100, Math.Round(v.DistanciaPercorridaKm / v.DistanciaTotalKm * 100, 1));

        // TempoRealHoras calculado se ainda em curso
        decimal? tempoReal = v.TempoRealHoras;
        if (v.Status == "EmCurso" && v.DataInicioReal.HasValue)
            tempoReal = (decimal)(DateTime.UtcNow - v.DataInicioReal.Value).TotalHours;

        return new GestaoViagemResponseDto
        {
            Id                    = v.Id,
            NumeroViagem          = v.NumeroViagem,
            Status                = v.Status,
            Prioridade            = v.Prioridade,
            DataCriacao           = v.DataCriacao,
            DataInicioPlaneada    = v.DataInicioPlaneada,
            DataFimPlaneada       = v.DataFimPlaneada,
            DataInicioReal        = v.DataInicioReal,
            DataFimReal           = v.DataFimReal,
            VeiculoId             = v.VeiculoId,
            VeiculoMatricula      = v.Veiculo?.Matricula,
            VeiculoMarca          = v.Veiculo?.Marca,
            VeiculoModelo         = v.Veiculo?.Modelo,
            MotoristaId           = v.MotoristaId,
            MotoristaNome         = v.Motorista?.Nome,
            TransportadoraId      = v.TransportadoraId,
            TransportadoraNome    = v.Transportadora?.Nome,
            ClienteId             = v.ClienteId,
            ClienteNome           = v.Cliente?.Nome,
            Origem                = v.Origem,
            Destino               = v.Destino,
            PrecoPorKm            = v.PrecoPorKm,
            CargaDescricao        = v.CargaDescricao,
            CargaPeso             = v.CargaPeso,
            CargaVolume           = v.CargaVolume,
            CargaObservacoes      = v.CargaObservacoes,
            DistanciaTotalKm      = v.DistanciaTotalKm,
            DistanciaPercorridaKm = v.DistanciaPercorridaKm,
            TempoEstimadoHoras    = v.TempoEstimadoHoras,
            TempoRealHoras        = tempoReal,
            AtrasoHoras           = atrasoHoras,
            ProgressoPercentual   = progresso,
            Observacoes           = v.Observacoes,
            CriadoEm              = v.CriadoEm,
            AtualizadoEm          = v.AtualizadoEm,
        };
    }
}
