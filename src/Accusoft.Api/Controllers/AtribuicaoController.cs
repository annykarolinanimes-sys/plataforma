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

    public AtribuicaoController(AppDbContext db) => _db = db;

    private static readonly HashSet<string> StatusFinais = ["Concluida", "Cancelada"];

    [HttpGet]
    public async Task<IActionResult> GetAtribuicoes(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);
        var uid  = User.GetUserId();

        var query = _db.Atribuicoes
            .AsNoTracking()
            .Include(a => a.Motorista)
            .Include(a => a.Veiculo)
            .Include(a => a.Transportadora)
            .Include(a => a.Entregas)
            .Where(a => a.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(a =>
                a.NumeroAtribuicao.ToLower().Contains(s) ||
                (a.ClienteNome  != null && a.ClienteNome.ToLower().Contains(s)) ||
                (a.Motorista    != null && a.Motorista.Nome.ToLower().Contains(s)) ||
                (a.Veiculo      != null && a.Veiculo.Matricula.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.DataAtribuicao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<AtribuicaoResponseDto>
        {
            Items    = items.Select(MapToResponseDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize,
        });
    }

    // ── GET /api/user/atribuicoes/{id} ────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAtribuicao(int id)
    {
        var uid        = User.GetUserId();
        var atribuicao = await FindAtribuicao(id, uid, includeAll: true);

        return atribuicao is null
            ? NotFound(new { message = "Atribuição não encontrada." })
            : Ok(MapToResponseDto(atribuicao));
    }

    // ── POST /api/user/atribuicoes ────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateAtribuicao([FromBody] AtribuicaoCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Erro de validação.",
                errors  = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        var uid = User.GetUserId();

        // FIX: verificar conflito de motorista
        if (dto.MotoristaId.HasValue)
        {
            var conflito = await MotoristaEmServico(dto.MotoristaId.Value, uid, excludeId: null);
            if (conflito)
                return Conflict(new
                {
                    message = "O motorista seleccionado já está em serviço numa atribuição 'Em Progresso'. " +
                              "Aguarde a conclusão da viagem actual antes de efectuar uma nova atribuição."
                });
        }

        // FIX: verificar conflito de veículo
        if (dto.VeiculoId.HasValue)
        {
            var conflito = await VeiculoEmServico(dto.VeiculoId.Value, uid, excludeId: null);
            if (conflito)
                return Conflict(new
                {
                    message = "O veículo seleccionado já está em uso numa atribuição 'Em Progresso'. " +
                              "Seleccione outro veículo ou aguarde a conclusão da operação actual."
                });
        }

        var now    = DateTimeOffset.UtcNow;
        var numero = GerarNumeroAtribuicao();

        var atribuicao = new Atribuicao
        {
            NumeroAtribuicao   = numero,
            DataAtribuicao     = DateTime.UtcNow,
            Status             = "Pendente",
            Prioridade         = dto.Prioridade,
            ClienteNome        = dto.ClienteNome.Trim(),
            ClienteContacto    = dto.ClienteContacto?.Trim(),
            EnderecoOrigem     = dto.EnderecoOrigem?.Trim(),
            EnderecoDestino    = dto.EnderecoDestino?.Trim(),
            DataPrevistaInicio = dto.DataPrevistaInicio,
            DataPrevistaFim    = dto.DataPrevistaFim,
            Observacoes        = dto.Observacoes?.Trim(),
            MotoristaId        = dto.MotoristaId,
            VeiculoId          = dto.VeiculoId,
            TransportadoraId   = dto.TransportadoraId,
            DistanciaTotalKm   = dto.DistanciaTotalKm,
            TempoEstimadoHoras = dto.TempoEstimadoHoras,
            UsuarioId          = uid,
            CriadoEm           = now,
            AtualizadoEm       = now,
        };

        _db.Atribuicoes.Add(atribuicao);
        await _db.SaveChangesAsync();

        if (dto.Entregas is { Count: > 0 })
        {
            foreach (var e in dto.Entregas)
            {
                _db.AtribuicaoEntregas.Add(new AtribuicaoEntrega
                {
                    AtribuicaoId = atribuicao.Id,
                    Destinatario = e.Destinatario?.Trim(),
                    Endereco     = e.Endereco?.Trim(),
                    Contacto     = e.Contacto?.Trim(),
                    Observacoes  = e.Observacoes?.Trim(),
                    Ordem        = e.Ordem,
                });
            }
            await _db.SaveChangesAsync();
        }

        var criada = await FindAtribuicao(atribuicao.Id, uid, includeAll: true);
        return CreatedAtAction(nameof(GetAtribuicao), new { id = atribuicao.Id }, MapToResponseDto(criada!));
    }

    // ── PUT /api/user/atribuicoes/{id} ────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAtribuicao(int id, [FromBody] AtribuicaoUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid        = User.GetUserId();
        var atribuicao = await FindAtribuicao(id, uid, includeAll: true);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        // FIX: bloqueia edição de atribuições finalizadas
        if (StatusFinais.Contains(atribuicao.Status))
            return BadRequest(new
            {
                message = $"Não é possível editar uma atribuição com status '{atribuicao.Status}'. " +
                           "Apenas atribuições Pendentes ou Em Progresso podem ser alteradas."
            });

        // FIX: verificar conflito de motorista (excluindo a própria atribuição)
        if (dto.MotoristaId.HasValue && dto.MotoristaId != atribuicao.MotoristaId)
        {
            var conflito = await MotoristaEmServico(dto.MotoristaId.Value, uid, excludeId: id);
            if (conflito)
                return Conflict(new
                {
                    message = "O motorista seleccionado já está em serviço numa atribuição 'Em Progresso'."
                });
        }

        // FIX: verificar conflito de veículo (excluindo a própria atribuição)
        if (dto.VeiculoId.HasValue && dto.VeiculoId != atribuicao.VeiculoId)
        {
            var conflito = await VeiculoEmServico(dto.VeiculoId.Value, uid, excludeId: id);
            if (conflito)
                return Conflict(new
                {
                    message = "O veículo seleccionado já está em uso numa atribuição 'Em Progresso'."
                });
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))         atribuicao.Status             = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Prioridade))     atribuicao.Prioridade         = dto.Prioridade;
        if (dto.DataPrevistaInicio.HasValue) atribuicao.DataPrevistaInicio = dto.DataPrevistaInicio.Value;
        if (dto.DataPrevistaFim.HasValue)    atribuicao.DataPrevistaFim    = dto.DataPrevistaFim.Value;
        if (dto.Observacoes is not null)     atribuicao.Observacoes        = dto.Observacoes.Trim();
        if (dto.MotoristaId.HasValue)        atribuicao.MotoristaId        = dto.MotoristaId.Value;
        if (dto.VeiculoId.HasValue)          atribuicao.VeiculoId          = dto.VeiculoId.Value;
        if (dto.TransportadoraId.HasValue)   atribuicao.TransportadoraId   = dto.TransportadoraId.Value;
        if (dto.DistanciaTotalKm.HasValue)   atribuicao.DistanciaTotalKm   = dto.DistanciaTotalKm.Value;
        if (dto.TempoEstimadoHoras.HasValue) atribuicao.TempoEstimadoHoras = dto.TempoEstimadoHoras.Value;

        // Actualizar entregas
        if (dto.Entregas is not null)
        {
            var idsActuais     = atribuicao.Entregas.Select(e => e.Id).ToHashSet();
            var idsActualizados = dto.Entregas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).ToHashSet();

            // Remover entregas eliminadas
            foreach (var e in atribuicao.Entregas.Where(e => !idsActualizados.Contains(e.Id)).ToList())
                _db.AtribuicaoEntregas.Remove(e);

            foreach (var eDto in dto.Entregas)
            {
                if (eDto.Id.HasValue)
                {
                    var existing = atribuicao.Entregas.FirstOrDefault(e => e.Id == eDto.Id.Value);
                    if (existing is not null)
                    {
                        if (eDto.Destinatario is not null) existing.Destinatario = eDto.Destinatario.Trim();
                        if (eDto.Endereco      is not null) existing.Endereco     = eDto.Endereco.Trim();
                        if (eDto.Contacto      is not null) existing.Contacto     = eDto.Contacto.Trim();
                        if (eDto.Observacoes   is not null) existing.Observacoes  = eDto.Observacoes.Trim();
                        if (eDto.Ordem.HasValue)            existing.Ordem        = eDto.Ordem.Value;
                        if (eDto.Realizada.HasValue)        existing.Realizada    = eDto.Realizada.Value;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(eDto.Destinatario))
                {
                    _db.AtribuicaoEntregas.Add(new AtribuicaoEntrega
                    {
                        AtribuicaoId = atribuicao.Id,
                        Destinatario = eDto.Destinatario.Trim(),
                        Endereco     = eDto.Endereco?.Trim(),
                        Contacto     = eDto.Contacto?.Trim(),
                        Observacoes  = eDto.Observacoes?.Trim(),
                        Ordem        = eDto.Ordem ?? 0,
                    });
                }
            }
        }

        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponseDto(atribuicao));
    }

    // ── DELETE /api/user/atribuicoes/{id} (Soft Delete → Cancelada) ───────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAtribuicao(int id)
    {
        var uid        = User.GetUserId();
        var atribuicao = await FindAtribuicao(id, uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        // FIX: bloqueia cancelamento de atribuições finalizadas
        if (StatusFinais.Contains(atribuicao.Status))
            return BadRequest(new
            {
                message = $"Não é possível cancelar uma atribuição com status '{atribuicao.Status}'."
            });

        atribuicao.Status       = "Cancelada";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Atribuição cancelada com sucesso." });
    }

    // ── POST /api/user/atribuicoes/{id}/iniciar ───────────────────────────────
    [HttpPost("{id:int}/iniciar")]
    public async Task<IActionResult> IniciarAtribuicao(int id)
    {
        var uid        = User.GetUserId();
        var atribuicao = await FindAtribuicao(id, uid);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        if (atribuicao.Status != "Pendente")
            return BadRequest(new { message = "Apenas atribuições Pendentes podem ser iniciadas." });

        // Re-verificar conflitos no momento de iniciar
        if (atribuicao.MotoristaId.HasValue)
        {
            var conflito = await MotoristaEmServico(atribuicao.MotoristaId.Value, uid, excludeId: id);
            if (conflito)
                return Conflict(new { message = "O motorista já está em serviço noutra atribuição." });
        }

        if (atribuicao.VeiculoId.HasValue)
        {
            var conflito = await VeiculoEmServico(atribuicao.VeiculoId.Value, uid, excludeId: id);
            if (conflito)
                return Conflict(new { message = "O veículo já está em uso noutra atribuição." });
        }

        atribuicao.Status       = "EmProgresso";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Atribuição iniciada com sucesso." });
    }

    // ── POST /api/user/atribuicoes/{id}/concluir ──────────────────────────────
    [HttpPost("{id:int}/concluir")]
    public async Task<IActionResult> ConcluirAtribuicao(int id)
    {
        var uid        = User.GetUserId();
        var atribuicao = await FindAtribuicao(id, uid, includeAll: true);

        if (atribuicao is null)
            return NotFound(new { message = "Atribuição não encontrada." });

        if (atribuicao.Status != "EmProgresso")
            return BadRequest(new { message = "Apenas atribuições Em Progresso podem ser concluídas." });

        atribuicao.Status       = "Concluida";
        atribuicao.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var entregasPendentes = atribuicao.Entregas.Count(e => !e.Realizada);
        var msg = entregasPendentes > 0
            ? $"Atribuição concluída com {entregasPendentes} entrega(s) por realizar."
            : "Atribuição concluída com sucesso. Todas as entregas realizadas.";

        return Ok(new { message = msg });
    }

    // ── Métodos privados ──────────────────────────────────────────────────────

    private async Task<Atribuicao?> FindAtribuicao(int id, int uid, bool includeAll = false)
    {
        var query = _db.Atribuicoes.Where(a => a.Id == id && a.UsuarioId == uid);

        if (includeAll)
            query = query
                .Include(a => a.Motorista)
                .Include(a => a.Veiculo)
                .Include(a => a.Transportadora)
                .Include(a => a.Entregas);

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// FIX: verifica se o motorista já está escalado numa atribuição EmProgresso.
    /// excludeId: ignora a própria atribuição durante um update.
    /// </summary>
    private async Task<bool> MotoristaEmServico(int motoristaId, int uid, int? excludeId)
    {
        return await _db.Atribuicoes
            .AsNoTracking()
            .AnyAsync(a =>
                a.UsuarioId   == uid           &&
                a.MotoristaId == motoristaId   &&
                a.Status      == "EmProgresso" &&
                (excludeId == null || a.Id != excludeId));
    }

    /// <summary>
    /// FIX: verifica se o veículo já está alocado a uma atribuição EmProgresso.
    /// excludeId: ignora a própria atribuição durante um update.
    /// </summary>
    private async Task<bool> VeiculoEmServico(int veiculoId, int uid, int? excludeId)
    {
        return await _db.Atribuicoes
            .AsNoTracking()
            .AnyAsync(a =>
                a.UsuarioId == uid         &&
                a.VeiculoId == veiculoId   &&
                a.Status    == "EmProgresso" &&
                (excludeId == null || a.Id != excludeId));
    }

    private string GerarNumeroAtribuicao()
    {
        var ano = DateTime.UtcNow.Year;
        var mes = DateTime.UtcNow.Month;
        var prefixo = $"ATRIB/{ano}/{mes:D2}/";

        var ultimo = _db.Atribuicoes
            .Where(a => a.NumeroAtribuicao.StartsWith(prefixo))
            .OrderByDescending(a => a.NumeroAtribuicao)
            .Select(a => a.NumeroAtribuicao)
            .FirstOrDefault();

        if (ultimo is null) return $"{prefixo}0001";

        var seq = int.TryParse(ultimo.Split('/').Last(), out var n) ? n : 0;
        return $"{prefixo}{(seq + 1):D4}";
    }

    private static AtribuicaoResponseDto MapToResponseDto(Atribuicao a) => new()
    {
        Id                  = a.Id,
        NumeroAtribuicao    = a.NumeroAtribuicao,
        DataAtribuicao      = a.DataAtribuicao,
        Status              = a.Status,
        Prioridade          = a.Prioridade,
        ClienteNome         = a.ClienteNome,
        ClienteContacto     = a.ClienteContacto,
        EnderecoOrigem      = a.EnderecoOrigem,
        EnderecoDestino     = a.EnderecoDestino,
        DataPrevistaInicio  = a.DataPrevistaInicio,
        DataPrevistaFim     = a.DataPrevistaFim,
        Observacoes         = a.Observacoes,
        MotoristaId         = a.MotoristaId,
        MotoristaNome       = a.Motorista?.Nome,
        VeiculoId           = a.VeiculoId,
        VeiculoMatricula    = a.Veiculo?.Matricula,
        VeiculoMarca        = a.Veiculo?.Marca,
        VeiculoModelo       = a.Veiculo?.Modelo,
        TransportadoraId    = a.TransportadoraId,
        TransportadoraNome  = a.Transportadora?.Nome,
        DistanciaTotalKm    = a.DistanciaTotalKm,
        TempoEstimadoHoras  = a.TempoEstimadoHoras,
        TotalEntregas       = a.Entregas?.Count                  ?? 0,
        EntregasRealizadas  = a.Entregas?.Count(e => e.Realizada) ?? 0,
        CriadoEm            = a.CriadoEm,
        AtualizadoEm        = a.AtualizadoEm,
        Entregas            = a.Entregas?.OrderBy(e => e.Ordem).Select(e => new AtribuicaoEntregaDto
        {
            Id           = e.Id,
            Destinatario = e.Destinatario,
            Endereco     = e.Endereco,
            Contacto     = e.Contacto,
            Observacoes  = e.Observacoes,
            Ordem        = e.Ordem,
            Realizada    = e.Realizada,
        }).ToList() ?? [],
    };
}
