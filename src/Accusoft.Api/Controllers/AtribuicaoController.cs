using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/atribuicoes")]
[Authorize]
public class AtribuicaoController : ControllerBase
{
    private readonly AppDbContext _db;

    public AtribuicaoController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/user/atribuicoes ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAtribuicoes(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.Atribuicoes!
            .AsNoTracking()
            .Include(a => a.Motorista)
            .Include(a => a.Veiculo)
            .Include(a => a.Transportadora)
            .Include(a => a.Rota)
            .Include(a => a.Ajudantes)
                .ThenInclude(aj => aj.Ajudante)
            .Include(a => a.Entregas)
            .Where(a => a.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(a =>
                a.NumeroAtribuicao.ToLower().Contains(s) ||
                (a.ClienteNome != null && a.ClienteNome.ToLower().Contains(s)) ||
                (a.Motorista != null && a.Motorista.Nome.ToLower().Contains(s)) ||
                (a.Veiculo != null && a.Veiculo.Matricula.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.DataAtribuicao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<AtribuicaoResponseDto>
        {
            Items = items.Select(MapToResponseDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    // ─── GET /api/user/atribuicoes/{id} ────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAtribuicao(int id)
    {
        var uid = User.GetUserId();
        var atribuicao = await _db.Atribuicoes!
            .AsNoTracking()
            .Include(a => a.Motorista)
            .Include(a => a.Veiculo)
            .Include(a => a.Transportadora)
            .Include(a => a.Rota)
            .Include(a => a.Ajudantes)
                .ThenInclude(aj => aj.Ajudante)
            .Include(a => a.Entregas)
            .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        return Ok(MapToResponseDto(atribuicao));
    }

    // ─── POST /api/user/atribuicoes ────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateAtribuicao([FromBody] AtribuicaoCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        var atribuicao = new Atribuicao
        {
            NumeroAtribuicao = GerarNumeroAtribuicao(),
            DataAtribuicao = DateTime.UtcNow,
            Status = "Pendente",
            Prioridade = dto.Prioridade,
            ClienteNome = dto.ClienteNome.Trim(),
            ClienteContacto = dto.ClienteContacto?.Trim(),
            EnderecoOrigem = dto.EnderecoOrigem?.Trim(),
            EnderecoDestino = dto.EnderecoDestino?.Trim(),
            DataPrevistaInicio = dto.DataPrevistaInicio,
            DataPrevistaFim = dto.DataPrevistaFim,
            Observacoes = dto.Observacoes?.Trim(),
            MotoristaId = dto.MotoristaId,
            VeiculoId = dto.VeiculoId,
            TransportadoraId = dto.TransportadoraId,
            RotaId = dto.RotaId,
            DistanciaTotalKm = dto.DistanciaTotalKm,
            TempoEstimadoHoras = dto.TempoEstimadoHoras,
            UsuarioId = uid,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.Atribuicoes!.Add(atribuicao);
        await _db.SaveChangesAsync();

        // Adicionar ajudantes
        if (dto.AjudanteIds != null && dto.AjudanteIds.Any())
        {
            foreach (var ajudanteId in dto.AjudanteIds)
            {
                _db.AtribuicaoAjudantes!.Add(new AtribuicaoAjudante
                {
                    AtribuicaoId = atribuicao.Id,
                    AjudanteId = ajudanteId
                });
            }
            await _db.SaveChangesAsync();
        }

        // Adicionar entregas
        if (dto.Entregas != null && dto.Entregas.Any())
        {
            foreach (var entregaDto in dto.Entregas)
            {
                _db.AtribuicaoEntregas!.Add(new AtribuicaoEntrega
                {
                    AtribuicaoId = atribuicao.Id,
                    Destinatario = entregaDto.Destinatario?.Trim(),
                    Endereco = entregaDto.Endereco?.Trim(),
                    Contacto = entregaDto.Contacto?.Trim(),
                    Observacoes = entregaDto.Observacoes?.Trim(),
                    Ordem = entregaDto.Ordem
                });
            }
            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetAtribuicao), new { id = atribuicao.Id }, MapToResponseDto(atribuicao));
    }

    // ─── PUT /api/user/atribuicoes/{id} ────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAtribuicao(int id, [FromBody] AtribuicaoUpdateDto dto)
    {
        var uid = User.GetUserId();
        var atribuicao = await _db.Atribuicoes!
            .Include(a => a.Ajudantes)
            .Include(a => a.Entregas)
            .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        if (!string.IsNullOrWhiteSpace(dto.Status))
            atribuicao.Status = dto.Status;

        if (!string.IsNullOrWhiteSpace(dto.Prioridade))
            atribuicao.Prioridade = dto.Prioridade;

        if (dto.DataPrevistaInicio.HasValue)
            atribuicao.DataPrevistaInicio = dto.DataPrevistaInicio.Value;

        if (dto.DataPrevistaFim.HasValue)
            atribuicao.DataPrevistaFim = dto.DataPrevistaFim.Value;

        if (!string.IsNullOrWhiteSpace(dto.Observacoes))
            atribuicao.Observacoes = dto.Observacoes.Trim();

        if (dto.MotoristaId.HasValue)
            atribuicao.MotoristaId = dto.MotoristaId.Value;

        if (dto.VeiculoId.HasValue)
            atribuicao.VeiculoId = dto.VeiculoId.Value;

        if (dto.TransportadoraId.HasValue)
            atribuicao.TransportadoraId = dto.TransportadoraId.Value;

        if (dto.RotaId.HasValue)
            atribuicao.RotaId = dto.RotaId.Value;

        if (dto.DistanciaTotalKm.HasValue)
            atribuicao.DistanciaTotalKm = dto.DistanciaTotalKm.Value;

        if (dto.TempoEstimadoHoras.HasValue)
            atribuicao.TempoEstimadoHoras = dto.TempoEstimadoHoras.Value;

        // Actualizar ajudantes
        if (dto.AjudanteIds != null)
        {
            var currentAjudantes = atribuicao.Ajudantes.Select(a => a.AjudanteId).ToList();
            var toRemove = currentAjudantes.Except(dto.AjudanteIds).ToList();
            var toAdd = dto.AjudanteIds.Except(currentAjudantes).ToList();

            foreach (var ajudanteId in toRemove)
            {
                var ajudante = atribuicao.Ajudantes.FirstOrDefault(a => a.AjudanteId == ajudanteId);
                if (ajudante != null)
                    _db.AtribuicaoAjudantes!.Remove(ajudante);
            }

            foreach (var ajudanteId in toAdd)
            {
                _db.AtribuicaoAjudantes!.Add(new AtribuicaoAjudante
                {
                    AtribuicaoId = atribuicao.Id,
                    AjudanteId = ajudanteId
                });
            }
        }

        // Actualizar entregas
        if (dto.Entregas != null)
        {
            var currentEntregaIds = atribuicao.Entregas.Select(e => e.Id).ToList();
            var updatedEntregaIds = dto.Entregas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).ToList();
            var toRemoveEntregas = currentEntregaIds.Except(updatedEntregaIds).ToList();

            foreach (var entregaId in toRemoveEntregas)
            {
                var entrega = atribuicao.Entregas.FirstOrDefault(e => e.Id == entregaId);
                if (entrega != null)
                    _db.AtribuicaoEntregas!.Remove(entrega);
            }

            foreach (var entregaDto in dto.Entregas)
            {
                if (entregaDto.Id.HasValue)
                {
                    var existingEntrega = atribuicao.Entregas.FirstOrDefault(e => e.Id == entregaDto.Id.Value);
                    if (existingEntrega != null)
                    {
                        if (!string.IsNullOrWhiteSpace(entregaDto.Destinatario))
                            existingEntrega.Destinatario = entregaDto.Destinatario.Trim();
                        if (!string.IsNullOrWhiteSpace(entregaDto.Endereco))
                            existingEntrega.Endereco = entregaDto.Endereco.Trim();
                        if (!string.IsNullOrWhiteSpace(entregaDto.Contacto))
                            existingEntrega.Contacto = entregaDto.Contacto.Trim();
                        if (!string.IsNullOrWhiteSpace(entregaDto.Observacoes))
                            existingEntrega.Observacoes = entregaDto.Observacoes.Trim();
                        if (entregaDto.Ordem.HasValue)
                            existingEntrega.Ordem = entregaDto.Ordem.Value;
                        if (entregaDto.Realizada.HasValue)
                            existingEntrega.Realizada = entregaDto.Realizada.Value;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entregaDto.Destinatario))
                {
                    _db.AtribuicaoEntregas!.Add(new AtribuicaoEntrega
                    {
                        AtribuicaoId = atribuicao.Id,
                        Destinatario = entregaDto.Destinatario.Trim(),
                        Endereco = entregaDto.Endereco?.Trim(),
                        Contacto = entregaDto.Contacto?.Trim(),
                        Observacoes = entregaDto.Observacoes?.Trim(),
                        Ordem = entregaDto.Ordem ?? 0
                    });
                }
            }
        }

        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(atribuicao));
    }

    // ─── DELETE /api/user/atribuicoes/{id} (Soft Delete) ───────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAtribuicao(int id)
    {
        var uid = User.GetUserId();
        var atribuicao = await _db.Atribuicoes!
            .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        atribuicao.Status = "Cancelada";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Atribuição cancelada com sucesso." });
    }

    // ─── POST /api/user/atribuicoes/{id}/iniciar ───────────────────────────────
    [HttpPost("{id:int}/iniciar")]
    public async Task<IActionResult> IniciarAtribuicao(int id)
    {
        var uid = User.GetUserId();
        var atribuicao = await _db.Atribuicoes!
            .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        if (atribuicao.Status != "Pendente")
            return BadRequest(new { message = "Apenas atribuições pendentes podem ser iniciadas." });

        atribuicao.Status = "EmProgresso";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Atribuição iniciada com sucesso." });
    }

    // ─── POST /api/user/atribuicoes/{id}/concluir ──────────────────────────────
    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirAtribuicao(int id)
    {
        var uid = User.GetUserId();
        var atribuicao = await _db.Atribuicoes!
            .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        if (atribuicao.Status != "EmProgresso")
            return BadRequest(new { message = "Apenas atribuições em progresso podem ser concluídas." });

        atribuicao.Status = "Concluida";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Atribuição concluída com sucesso." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private string GerarNumeroAtribuicao()
    {
        var ano = DateTime.Now.Year;
        var mes = DateTime.Now.Month;
        var ultima = _db.Atribuicoes!
            .Where(a => a.NumeroAtribuicao.StartsWith($"ATRIB/{ano}/{mes}/"))
            .OrderByDescending(a => a.NumeroAtribuicao)
            .FirstOrDefault();

        if (ultima is null)
            return $"ATRIB/{ano}/{mes}/0001";

        var ultimoNumero = int.Parse(ultima.NumeroAtribuicao.Split('/').Last());
        return $"ATRIB/{ano}/{mes}/{ultimoNumero + 1:D4}";
    }

    private static AtribuicaoResponseDto MapToResponseDto(Atribuicao a)
    {
        return new AtribuicaoResponseDto
        {
            Id = a.Id,
            NumeroAtribuicao = a.NumeroAtribuicao,
            DataAtribuicao = a.DataAtribuicao,
            Status = a.Status,
            Prioridade = a.Prioridade,
            ClienteNome = a.ClienteNome,
            ClienteContacto = a.ClienteContacto,
            EnderecoOrigem = a.EnderecoOrigem,
            EnderecoDestino = a.EnderecoDestino,
            DataPrevistaInicio = a.DataPrevistaInicio,
            DataPrevistaFim = a.DataPrevistaFim,
            Observacoes = a.Observacoes,
            MotoristaId = a.MotoristaId,
            MotoristaNome = a.Motorista?.Nome,
            VeiculoId = a.VeiculoId,
            VeiculoMatricula = a.Veiculo?.Matricula,
            VeiculoMarca = a.Veiculo?.Marca,
            VeiculoModelo = a.Veiculo?.Modelo,
            TransportadoraId = a.TransportadoraId,
            TransportadoraNome = a.Transportadora?.Nome,
            RotaId = a.RotaId,
            RotaNome = a.Rota?.Nome,
            AjudanteIds = a.Ajudantes?.Select(aj => aj.AjudanteId).ToList() ?? [],
            AjudanteNomes = a.Ajudantes?.Select(aj => aj.Ajudante?.Nome ?? "").ToList() ?? [],
            DistanciaTotalKm = a.DistanciaTotalKm,
            TempoEstimadoHoras = a.TempoEstimadoHoras,
            TotalEntregas = a.Entregas?.Count ?? 0,
            EntregasRealizadas = a.Entregas?.Count(e => e.Realizada) ?? 0,
            CriadoEm = a.CriadoEm,
            AtualizadoEm = a.AtualizadoEm,
            Entregas = a.Entregas?.Select(e => new AtribuicaoEntregaDto
            {
                Id = e.Id,
                Destinatario = e.Destinatario,
                Endereco = e.Endereco,
                Contacto = e.Contacto,
                Observacoes = e.Observacoes,
                Ordem = e.Ordem,
                Realizada = e.Realizada
            }).ToList() ?? []
        };
    }
}