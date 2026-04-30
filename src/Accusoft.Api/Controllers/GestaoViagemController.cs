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

    public GestaoViagemController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/gestao-viagens ──────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetViagens(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.GestaoViagens!
            .AsNoTracking()
            .Where(v => v.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(v => v.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                v.NumeroViagem.ToLower().Contains(s) ||
                (v.Rota != null && v.Rota.Nome.ToLower().Contains(s)) ||
                (v.Motorista != null && v.Motorista.Nome.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Carregar relacionamentos separadamente
        var rotaIds = items.Where(v => v.RotaId.HasValue).Select(v => v.RotaId!.Value).Distinct().ToList();
        var veiculoIds = items.Where(v => v.VeiculoId.HasValue).Select(v => v.VeiculoId!.Value).Distinct().ToList();
        var motoristaIds = items.Where(v => v.MotoristaId.HasValue).Select(v => v.MotoristaId!.Value).Distinct().ToList();
        var transportadoraIds = items.Where(v => v.TransportadoraId.HasValue).Select(v => v.TransportadoraId!.Value).Distinct().ToList();

        var rotas = rotaIds.Any() 
            ? await _db.RotasCatalogo!.Where(r => rotaIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id) 
            : new Dictionary<int, RotaCatalogo>();
        
        var veiculos = veiculoIds.Any() 
            ? await _db.Veiculos!.Where(v => veiculoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id) 
            : new Dictionary<int, Veiculo>();
        
        var motoristas = motoristaIds.Any() 
            ? await _db.Users!.Where(u => motoristaIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id) 
            : new Dictionary<int, User>();
        
        var transportadoras = transportadoraIds.Any() 
            ? await _db.TransportadorasCatalogo!.Where(t => transportadoraIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id) 
            : new Dictionary<int, TransportadoraCatalogo>();

        var result = new PagedResult<GestaoViagemResponseDto>
        {
            Items = items.Select(v => MapToResponseDto(v, rotas, veiculos, motoristas, transportadoras)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/gestao-viagens/{id} ─────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetViagem(int id)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        var rota = viagem.RotaId.HasValue ? await _db.RotasCatalogo!.FindAsync(viagem.RotaId.Value) : null;
        var veiculo = viagem.VeiculoId.HasValue ? await _db.Veiculos!.FindAsync(viagem.VeiculoId.Value) : null;
        var motorista = viagem.MotoristaId.HasValue ? await _db.Users!.FindAsync(viagem.MotoristaId.Value) : null;
        var transportadora = viagem.TransportadoraId.HasValue ? await _db.TransportadorasCatalogo!.FindAsync(viagem.TransportadoraId.Value) : null;

        var rotas = rota != null ? new Dictionary<int, RotaCatalogo> { { rota.Id, rota } } : new Dictionary<int, RotaCatalogo>();
        var veiculos = veiculo != null ? new Dictionary<int, Veiculo> { { veiculo.Id, veiculo } } : new Dictionary<int, Veiculo>();
        var motoristas = motorista != null ? new Dictionary<int, User> { { motorista.Id, motorista } } : new Dictionary<int, User>();
        var transportadoras = transportadora != null ? new Dictionary<int, TransportadoraCatalogo> { { transportadora.Id, transportadora } } : new Dictionary<int, TransportadoraCatalogo>();

        return Ok(MapToResponseDto(viagem, rotas, veiculos, motoristas, transportadoras));
    }

    // ─── POST /api/user/gestao-viagens ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateViagem([FromBody] GestaoViagemCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        // Validações adicionais
        if (dto.DataInicioPlaneada.HasValue && dto.DataFimPlaneada.HasValue && 
            dto.DataFimPlaneada.Value < dto.DataInicioPlaneada.Value)
        {
            return BadRequest(new { message = "Data/Hora de fim não pode ser anterior à data/hora de início." });
        }

        var viagem = new GestaoViagem
        {
            NumeroViagem = GerarNumeroViagem(),
            Status = "Planeada",
            Prioridade = dto.Prioridade,
            DataCriacao = DateTime.UtcNow,
            DataInicioPlaneada = dto.DataInicioPlaneada,
            DataFimPlaneada = dto.DataFimPlaneada,
            RotaId = dto.RotaId,
            VeiculoId = dto.VeiculoId,
            MotoristaId = dto.MotoristaId,
            TransportadoraId = dto.TransportadoraId,
            CargaDescricao = dto.CargaDescricao,
            CargaPeso = dto.CargaPeso,
            CargaVolume = dto.CargaVolume,
            CargaObservacoes = dto.CargaObservacoes,
            DistanciaTotalKm = dto.DistanciaTotalKm,
            TempoEstimadoHoras = dto.TempoEstimadoHoras,
            Observacoes = dto.Observacoes,
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.GestaoViagens!.Add(viagem);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetViagem), new { id = viagem.Id }, 
            MapToResponseDto(viagem, new Dictionary<int, RotaCatalogo>(), new Dictionary<int, Veiculo>(), 
                new Dictionary<int, User>(), new Dictionary<int, TransportadoraCatalogo>()));
    }

    // ─── PUT /api/user/gestao-viagens/{id} ─────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateViagem(int id, [FromBody] GestaoViagemUpdateDto dto)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        // Validação de datas
        if (dto.DataInicioPlaneada.HasValue && dto.DataFimPlaneada.HasValue && 
            dto.DataFimPlaneada.Value < dto.DataInicioPlaneada.Value)
        {
            return BadRequest(new { message = "Data/Hora de fim não pode ser anterior à data/hora de início." });
        }

        if (dto.DataInicioReal.HasValue && dto.DataFimReal.HasValue && 
            dto.DataFimReal.Value < dto.DataInicioReal.Value)
        {
            return BadRequest(new { message = "Data/Hora de fim real não pode ser anterior à data/hora de início real." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))
            viagem.Status = dto.Status;

        if (!string.IsNullOrWhiteSpace(dto.Prioridade))
            viagem.Prioridade = dto.Prioridade;

        if (dto.DataInicioPlaneada.HasValue)
            viagem.DataInicioPlaneada = dto.DataInicioPlaneada.Value;

        if (dto.DataFimPlaneada.HasValue)
            viagem.DataFimPlaneada = dto.DataFimPlaneada.Value;

        if (dto.DataInicioReal.HasValue)
            viagem.DataInicioReal = dto.DataInicioReal.Value;

        if (dto.DataFimReal.HasValue)
            viagem.DataFimReal = dto.DataFimReal.Value;

        if (dto.RotaId.HasValue)
            viagem.RotaId = dto.RotaId.Value;

        if (dto.VeiculoId.HasValue)
            viagem.VeiculoId = dto.VeiculoId.Value;

        if (dto.MotoristaId.HasValue)
            viagem.MotoristaId = dto.MotoristaId.Value;

        if (dto.TransportadoraId.HasValue)
            viagem.TransportadoraId = dto.TransportadoraId.Value;

        if (!string.IsNullOrWhiteSpace(dto.CargaDescricao))
            viagem.CargaDescricao = dto.CargaDescricao;

        if (dto.CargaPeso.HasValue)
            viagem.CargaPeso = dto.CargaPeso.Value;

        if (dto.CargaVolume.HasValue)
            viagem.CargaVolume = dto.CargaVolume.Value;

        if (!string.IsNullOrWhiteSpace(dto.CargaObservacoes))
            viagem.CargaObservacoes = dto.CargaObservacoes;

        if (dto.DistanciaTotalKm.HasValue)
            viagem.DistanciaTotalKm = dto.DistanciaTotalKm.Value;

        if (dto.DistanciaPercorridaKm.HasValue)
            viagem.DistanciaPercorridaKm = dto.DistanciaPercorridaKm.Value;

        if (dto.TempoEstimadoHoras.HasValue)
            viagem.TempoEstimadoHoras = dto.TempoEstimadoHoras.Value;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            viagem.Observacoes = dto.Observacoes;

        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(viagem, new Dictionary<int, RotaCatalogo>(), new Dictionary<int, Veiculo>(), 
            new Dictionary<int, User>(), new Dictionary<int, TransportadoraCatalogo>()));
    }

    // ─── POST /api/user/gestao-viagens/{id}/iniciar ────────────────────────────
    [HttpPost("{id:int}/iniciar")]
    public async Task<IActionResult> IniciarViagem(int id)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (viagem.Status != "Planeada")
            return BadRequest(new { message = "Apenas viagens planeadas podem ser iniciadas." });

        viagem.Status = "EmCurso";
        viagem.DataInicioReal = DateTime.UtcNow;
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Viagem iniciada com sucesso.", viagemId = viagem.Id, status = viagem.Status });
    }

    // ─── POST /api/user/gestao-viagens/{id}/concluir ───────────────────────────
    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirViagem(int id)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (viagem.Status != "EmCurso")
            return BadRequest(new { message = "Apenas viagens em curso podem ser concluídas." });

        viagem.Status = "Concluida";
        viagem.DataFimReal = DateTime.UtcNow;
        
        // Calcular distância percorrida se não fornecida
        if (viagem.DistanciaPercorridaKm == 0 && viagem.DistanciaTotalKm > 0)
            viagem.DistanciaPercorridaKm = viagem.DistanciaTotalKm;
        
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Viagem concluída com sucesso.", viagemId = viagem.Id, status = viagem.Status });
    }

    // ─── POST /api/user/gestao-viagens/{id}/atualizar-km ───────────────────────
    [HttpPost("{id:int}/atualizar-km")]
    public async Task<IActionResult> AtualizarKm(int id, [FromBody] AtualizarKmRequest request)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (request.DistanciaPercorridaKm < 0)
            return BadRequest(new { message = "Distância percorrida não pode ser negativa." });

        viagem.DistanciaPercorridaKm = request.DistanciaPercorridaKm;
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Quilometragem atualizada com sucesso.", distanciaPercorridaKm = viagem.DistanciaPercorridaKm });
    }

    // ─── DELETE /api/user/gestao-viagens/{id} ──────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteViagem(int id)
    {
        var uid = User.GetUserId();
        var viagem = await _db.GestaoViagens!
            .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioId == uid);

        if (viagem is null)
            return NotFound(new { message = "Viagem não encontrada." });

        if (viagem.Status == "EmCurso")
            return BadRequest(new { message = "Não é possível cancelar uma viagem em curso. Conclua ou aguarde." });

        viagem.Status = "Cancelada";
        viagem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Viagem cancelada com sucesso." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroViagem()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultima = _db.GestaoViagens!
            .Where(v => v.NumeroViagem.StartsWith($"VIA/{ano}/{mes}/"))
            .OrderByDescending(v => v.NumeroViagem)
            .FirstOrDefault();

        if (ultima is null)
            return $"VIA/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultima.NumeroViagem.Split('/').Last());
        return $"VIA/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private static GestaoViagemResponseDto MapToResponseDto(
        GestaoViagem v,
        Dictionary<int, RotaCatalogo> rotas,
        Dictionary<int, Veiculo> veiculos,
        Dictionary<int, User> motoristas,
        Dictionary<int, TransportadoraCatalogo> transportadoras)
    {
        // Calcular tempos
        var tempoPlaneado = v.DataInicioPlaneada.HasValue && v.DataFimPlaneada.HasValue
            ? (v.DataFimPlaneada.Value - v.DataInicioPlaneada.Value).TotalHours
            : (double?)null;

        var tempoReal = v.DataInicioReal.HasValue && v.DataFimReal.HasValue
            ? (v.DataFimReal.Value - v.DataInicioReal.Value).TotalHours
            : (double?)null;

        var progresso = v.DistanciaTotalKm > 0 
            ? (v.DistanciaPercorridaKm / v.DistanciaTotalKm) * 100 
            : 0;

        var atrasoHoras = v.TempoEstimadoHoras.HasValue && tempoReal.HasValue && tempoReal.Value > (double)v.TempoEstimadoHoras.Value
            ? tempoReal.Value - (double)v.TempoEstimadoHoras.Value
            : (double?)null;

        return new GestaoViagemResponseDto
        {
            Id = v.Id,
            NumeroViagem = v.NumeroViagem,
            Status = v.Status,
            Prioridade = v.Prioridade,
            DataCriacao = v.DataCriacao,
            DataInicioPlaneada = v.DataInicioPlaneada,
            DataFimPlaneada = v.DataFimPlaneada,
            DataInicioReal = v.DataInicioReal,
            DataFimReal = v.DataFimReal,
            RotaId = v.RotaId,
            RotaNome = v.RotaId.HasValue && rotas.TryGetValue(v.RotaId.Value, out var r) ? r.Nome : null,
            VeiculoId = v.VeiculoId,
            VeiculoMatricula = v.VeiculoId.HasValue && veiculos.TryGetValue(v.VeiculoId.Value, out var ve) ? ve.Matricula : null,
            VeiculoMarca = v.VeiculoId.HasValue && veiculos.TryGetValue(v.VeiculoId.Value, out var ve2) ? ve2.Marca : null,
            VeiculoModelo = v.VeiculoId.HasValue && veiculos.TryGetValue(v.VeiculoId.Value, out var ve3) ? ve3.Modelo : null,
            MotoristaId = v.MotoristaId,
            MotoristaNome = v.MotoristaId.HasValue && motoristas.TryGetValue(v.MotoristaId.Value, out var m) ? m.Nome : null,
            TransportadoraId = v.TransportadoraId,
            TransportadoraNome = v.TransportadoraId.HasValue && transportadoras.TryGetValue(v.TransportadoraId.Value, out var t) ? t.Nome : null,
            CargaDescricao = v.CargaDescricao,
            CargaPeso = v.CargaPeso,
            CargaVolume = v.CargaVolume,
            CargaObservacoes = v.CargaObservacoes,
            DistanciaTotalKm = v.DistanciaTotalKm,
            DistanciaPercorridaKm = v.DistanciaPercorridaKm,
            TempoEstimadoHoras = v.TempoEstimadoHoras.HasValue ? (decimal)v.TempoEstimadoHoras.Value : null,
            TempoRealHoras = tempoReal.HasValue ? (decimal)tempoReal.Value : null,
            AtrasoHoras = atrasoHoras.HasValue ? (decimal)atrasoHoras.Value : null,
            ProgressoPercentual = Math.Min(100, Math.Max(0, (decimal)progresso)),
            Observacoes = v.Observacoes,
            CriadoEm = v.CriadoEm,
            AtualizadoEm = v.AtualizadoEm
        };
    }
}

// ─── Request DTO para atualização de quilometragem ────────────────────────────
public record AtualizarKmRequest(decimal DistanciaPercorridaKm);