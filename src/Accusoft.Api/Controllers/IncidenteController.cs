using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/incidentes")]
[Authorize]
public class IncidenteController(AppDbContext db, IFileStorageService fileStorage) : ControllerBase
{
    private static readonly HashSet<string> StatusFinais = ["Resolvido", "Fechado"];

    // ── Tipos de ficheiro permitidos ──────────────────────────────────────────
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/octet-stream",
        "application/x-pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/x-png"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private const long MaxAttachmentBytes = 5 * 1024 * 1024; // 5 MB

    // ── GET /api/user/incidentes ──────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetIncidentes(
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] string? gravidade,
        [FromQuery] int?    viagemId,
        [FromQuery] int?    veiculoId,
        [FromQuery] int?    clienteId,
        [FromQuery] string? search,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 15)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);
        var uid  = User.GetUserId();

        var query = db.Incidentes
            .AsNoTracking()
            .Include(i => i.Viagem)
            .Include(i => i.Veiculo)
            .Include(i => i.Cliente)
            .Include(i => i.Atribuicao)
            .Include(i => i.Anexos)   
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(i => i.UsuarioId == uid);

        if (!string.IsNullOrWhiteSpace(tipo))      query = query.Where(i => i.Tipo      == tipo);
        if (!string.IsNullOrWhiteSpace(status))    query = query.Where(i => i.Status    == status);
        if (!string.IsNullOrWhiteSpace(gravidade)) query = query.Where(i => i.Gravidade == gravidade);
        if (viagemId.HasValue)  query = query.Where(i => i.ViagemId  == viagemId.Value);
        if (veiculoId.HasValue) query = query.Where(i => i.VeiculoId == veiculoId.Value);
        if (clienteId.HasValue) query = query.Where(i => i.ClienteId == clienteId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i =>
                i.NumeroIncidente.ToLower().Contains(s) ||
                i.Titulo.ToLower().Contains(s)          ||
                (i.Descricao != null && i.Descricao.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.DataOcorrencia)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<IncidenteResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize,
        });
    }

    // ── GET /api/user/incidentes/{id} ─────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetIncidente(int id)
    {
        var uid       = User.GetUserId();
        var incidente = await FindIncidente(id, uid);

        return incidente is null
            ? NotFound(new { message = "Incidente não encontrado." })
            : Ok(MapToDto(incidente));
    }

    // ── GET /api/user/incidentes/por-viagem/{viagemId} ────────────────────────

    [HttpGet("por-viagem/{viagemId:int}")]
    public async Task<IActionResult> GetPorViagem(int viagemId)
    {
        var uid  = User.GetUserId();
        var query = db.Incidentes
            .AsNoTracking()
            .Include(i => i.Viagem)
            .Include(i => i.Veiculo)
            .Include(i => i.Anexos)   // ← NOVO
            .Where(i => i.ViagemId == viagemId);

        if (!User.IsAdmin())
            query = query.Where(i => i.UsuarioId == uid);

        var list = await query
            .OrderByDescending(i => i.DataOcorrencia)
            .ToListAsync();

        return Ok(list.Select(MapToDto));
    }

    // ── POST /api/user/incidentes ─────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateIncidente([FromBody] IncidenteCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Erro de validação.",
                errors  = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        if (!dto.ViagemId.HasValue && !dto.VeiculoId.HasValue &&
            !dto.ClienteId.HasValue && !dto.AtribuicaoId.HasValue)
            return BadRequest(new
            {
                message = "Associe pelo menos um vínculo: Viagem, Veículo, Cliente ou Atribuição."
            });

        var uid = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var incidente = new Incidente
            {
                NumeroIncidente      = await GerarNumeroIncidente(uid),
                DataOcorrencia       = dto.DataOcorrencia.HasValue
                    ? DateTime.SpecifyKind(dto.DataOcorrencia.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                Tipo                 = dto.Tipo,
                Gravidade            = dto.Gravidade,
                Status               = "Aberto",
                Titulo               = dto.Titulo.Trim(),
                Descricao            = dto.Descricao?.Trim(),
                ViagemId             = dto.ViagemId,
                VeiculoId            = dto.VeiculoId,
                ClienteId            = dto.ClienteId,
                AtribuicaoId         = dto.AtribuicaoId,
                Causa                = dto.Causa?.Trim(),
                AcaoCorretiva        = dto.AcaoCorretiva?.Trim(),
                ResponsavelResolucao = dto.ResponsavelResolucao?.Trim(),
                CustoAssociado       = dto.CustoAssociado,
                Observacoes          = dto.Observacoes?.Trim(),
                UsuarioId            = uid,
                CriadoEm             = now,
                AtualizadoEm         = now,
            };

            db.Incidentes.Add(incidente);
            await db.SaveChangesAsync();

            // Desativar veículo automaticamente em caso de avaria
            if (dto.Tipo == "Avaria" && dto.VeiculoId.HasValue)
            {
                var veiculoQuery = db.Veiculos.Where(v => v.Id == dto.VeiculoId.Value);
                if (!User.IsAdmin())
                    veiculoQuery = veiculoQuery.Where(v => v.CriadoPor == uid);
                var veiculo = await veiculoQuery.FirstOrDefaultAsync();

                if (veiculo is not null && veiculo.Ativo)
                {
                    veiculo.Ativo        = false;
                    veiculo.AtualizadoEm = now;
                    await db.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();

            var criado = await FindIncidente(incidente.Id, uid);
            return CreatedAtAction(nameof(GetIncidente), new { id = incidente.Id }, MapToDto(criado!));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = $"Erro ao criar incidente: {ex.Message}" });
        }
    }


    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateIncidente(int id, [FromBody] IncidenteUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid       = User.GetUserId();
        var incidente = await FindIncidente(id, uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (incidente.Status == "Fechado")
            return BadRequest(new { message = "Incidentes fechados não podem ser editados." });

        if (!string.IsNullOrWhiteSpace(dto.Status))    incidente.Status    = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Gravidade)) incidente.Gravidade = dto.Gravidade;
        if (dto.Descricao            is not null) incidente.Descricao            = dto.Descricao.Trim();
        if (dto.Causa                is not null) incidente.Causa                = dto.Causa.Trim();
        if (dto.AcaoCorretiva        is not null) incidente.AcaoCorretiva        = dto.AcaoCorretiva.Trim();
        if (dto.ResponsavelResolucao is not null) incidente.ResponsavelResolucao = dto.ResponsavelResolucao.Trim();
        if (dto.CustoAssociado.HasValue)          incidente.CustoAssociado       = dto.CustoAssociado.Value;
        if (dto.Observacoes          is not null) incidente.Observacoes          = dto.Observacoes.Trim();
        if (dto.DataResolucao.HasValue)           incidente.DataResolucao        = DateTime.SpecifyKind(dto.DataResolucao.Value, DateTimeKind.Utc);

        incidente.AtualizadoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var updated = await FindIncidente(id, uid);
        return Ok(MapToDto(updated!));
    }


    [HttpPost("{id:int}/resolver")]
    public async Task<IActionResult> ResolverIncidente(int id, [FromBody] ResolverIncidenteDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Erro de validação.",
                errors  = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        var uid       = User.GetUserId();
        var incidente = await FindIncidente(id, uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (StatusFinais.Contains(incidente.Status))
            return BadRequest(new { message = "Incidente já foi resolvido ou fechado." });

        if (string.IsNullOrWhiteSpace(dto.AcaoCorretiva))
            return BadRequest(new { message = "A ação corretiva é obrigatória para resolver o incidente." });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var agora = DateTimeOffset.UtcNow;

            incidente.Status               = "Resolvido";
            incidente.DataResolucao        = DateTime.UtcNow;
            incidente.AcaoCorretiva        = dto.AcaoCorretiva.Trim();
            incidente.ResponsavelResolucao = dto.ResponsavelResolucao?.Trim();
            incidente.CustoAssociado       = dto.CustoAssociado;
            incidente.Observacoes          = dto.Observacoes?.Trim() ?? incidente.Observacoes;
            incidente.AtualizadoEm         = agora;

            await db.SaveChangesAsync();

            // Reativar veículo ao resolver avaria
            if (incidente.Tipo == "Avaria" && incidente.VeiculoId.HasValue)
            {
                var veiculoQuery = db.Veiculos.Where(v => v.Id == incidente.VeiculoId.Value);
                if (!User.IsAdmin())
                    veiculoQuery = veiculoQuery.Where(v => v.CriadoPor == uid);
                var veiculo = await veiculoQuery.FirstOrDefaultAsync();

                if (veiculo is not null && !veiculo.Ativo)
                {
                    veiculo.Ativo        = true;
                    veiculo.AtualizadoEm = agora;
                    await db.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();
            return Ok(new { message = "Incidente resolvido com sucesso.", incidenteId = incidente.Id });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = $"Erro ao resolver incidente: {ex.Message}" });
        }
    }


    [HttpPost("{id:int}/fechar")]
    public async Task<IActionResult> FecharIncidente(int id)
    {
        var uid       = User.GetUserId();
        var incidente = await FindIncidente(id, uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        if (incidente.Status != "Resolvido")
            return BadRequest(new { message = "Apenas incidentes resolvidos podem ser fechados." });

        incidente.Status       = "Fechado";
        incidente.AtualizadoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Incidente fechado com sucesso." });
    }


    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteIncidente(int id)
    {
        var uid       = User.GetUserId();
        var incidente = await FindIncidente(id, uid);

        if (incidente is null)
            return NotFound(new { message = "Incidente não encontrado." });

        foreach (var anexo in incidente.Anexos)
        {
            try { fileStorage.Delete(anexo.PathUrl); }
            catch { /* ignora falhas de limpeza */ }
        }

        db.IncidenteAnexos.RemoveRange(incidente.Anexos);
        db.Incidentes.Remove(incidente);
        await db.SaveChangesAsync();

        return Ok(new { message = "Incidente eliminado com sucesso." });
    }

    [HttpPost("{id:int}/anexos")]
    [RequestSizeLimit(5 * 1024 * 1024 + 1024)]
    public async Task<IActionResult> UploadAnexo(int id, [FromForm] IFormFile ficheiro)
    {
        if (ficheiro is null || ficheiro.Length == 0)
            return BadRequest(new { message = "Ficheiro é obrigatório." });

        if (ficheiro.Length > MaxAttachmentBytes)
            return BadRequest(new { message = "O ficheiro não pode exceder 5 MB." });

        var ext = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Extensão de ficheiro não permitida. Use PDF, JPG ou PNG." });

        if (!AllowedMimeTypes.Contains(ficheiro.ContentType))
        {
            var mime      = ficheiro.ContentType?.ToLowerInvariant() ?? string.Empty;
            var isPdfAlias   = ext == ".pdf"  && (mime == "application/octet-stream" || mime == "application/x-pdf");
            var isImageAlias = (ext is ".jpg" or ".jpeg" or ".png") && mime.StartsWith("image/");

            if (!isPdfAlias && !isImageAlias)
                return BadRequest(new { message = "Apenas ficheiros PDF, JPG ou PNG são permitidos." });
        }

        var uid = User.GetUserId();

        // Verificar que o incidente existe e pertence ao utilizador
        var incidenteQuery = db.Incidentes.AsQueryable();
        if (!User.IsAdmin())
            incidenteQuery = incidenteQuery.Where(i => i.UsuarioId == uid);

        var incidenteExiste = await incidenteQuery.AnyAsync(i => i.Id == id);
        if (!incidenteExiste)
            return NotFound(new { message = "Incidente não encontrado." });

        var (pathUrl, _, _) = await fileStorage.SaveAsync(ficheiro, uid);

        var anexo = new IncidenteAnexo
        {
            IncidenteId  = id,
            NomeOriginal = Path.GetFileName(ficheiro.FileName),
            NomeFicheiro = Path.GetFileName(pathUrl),
            PathUrl      = pathUrl,
            MimeType     = ficheiro.ContentType ?? string.Empty,
            TamanhoBytes = ficheiro.Length,
            UsuarioId    = uid,
            CriadoEm     = DateTimeOffset.UtcNow
        };

        db.IncidenteAnexos.Add(anexo);
        await db.SaveChangesAsync();

        return Ok(MapAnexoDto(anexo));
    }

    /// <summary>
    /// GET /api/user/incidentes/{id}/anexos/{anexoId}
    /// Faz download de um anexo do incidente.
    /// </summary>
    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> DownloadAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        var incidenteQuery = db.Incidentes.AsQueryable();
        if (!User.IsAdmin())
            incidenteQuery = incidenteQuery.Where(i => i.UsuarioId == uid);

        var incidenteExiste = await incidenteQuery.AnyAsync(i => i.Id == id);
        if (!incidenteExiste)
            return NotFound(new { message = "Incidente não encontrado." });

        var anexo = await db.IncidenteAnexos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.IncidenteId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        var stream = fileStorage.GetStream(anexo.PathUrl);
        if (stream is null)
            return NotFound(new { message = "Ficheiro não encontrado no servidor." });

        return File(stream, anexo.MimeType, anexo.NomeOriginal);
    }

    /// <summary>
    /// DELETE /api/user/incidentes/{id}/anexos/{anexoId}
    /// Remove um anexo do incidente.
    /// </summary>
    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> RemoverAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        var incidenteQuery = db.Incidentes.AsQueryable();
        if (!User.IsAdmin())
            incidenteQuery = incidenteQuery.Where(i => i.UsuarioId == uid);

        var incidenteExiste = await incidenteQuery.AnyAsync(i => i.Id == id);
        if (!incidenteExiste)
            return NotFound(new { message = "Incidente não encontrado." });

        var anexo = await db.IncidenteAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.IncidenteId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        try { fileStorage.Delete(anexo.PathUrl); }
        catch { /* ignora falhas de limpeza de ficheiros */ }

        db.IncidenteAnexos.Remove(anexo);
        await db.SaveChangesAsync();

        return Ok(new { message = "Anexo removido com sucesso." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Incidente?> FindIncidente(int id, int uid)
    {
        var query = db.Incidentes
            .Include(i => i.Viagem)
            .Include(i => i.Veiculo)
            .Include(i => i.Cliente)
            .Include(i => i.Atribuicao)
            .Include(i => i.Anexos)   // ← NOVO
            .Where(i => i.Id == id);

        if (!User.IsAdmin())
            query = query.Where(i => i.UsuarioId == uid);

        return await query.FirstOrDefaultAsync();
    }

    private async Task<string> GerarNumeroIncidente(int userId)
    {
        var agora   = DateTime.UtcNow;
        var prefixo = $"INC-{agora:yyyyMM}-";

        var existentes = await db.Incidentes
            .Where(i => i.UsuarioId == userId && i.NumeroIncidente.StartsWith(prefixo))
            .Select(i => i.NumeroIncidente)
            .ToListAsync();

        var maxSeq = existentes
            .Select(n => {
                var parts = n.Split('-');
                return parts.Length == 3 && int.TryParse(parts[2], out var s) ? s : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefixo}{(maxSeq + 1):D4}";
    }

    private static string FormatarTamanho(long bytes)
    {
        if (bytes < 1024)        return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private static IncidenteAnexoDto MapAnexoDto(IncidenteAnexo a) => new(
        a.Id,
        a.NomeOriginal,
        a.MimeType,
        a.TamanhoBytes,
        FormatarTamanho(a.TamanhoBytes),
        a.CriadoEm
    );

    private static IncidenteResponseDto MapToDto(Incidente i) => new()
    {
        Id                   = i.Id,
        NumeroIncidente      = i.NumeroIncidente,
        DataOcorrencia       = i.DataOcorrencia,
        Tipo                 = i.Tipo,
        Gravidade            = i.Gravidade,
        Status               = i.Status,
        Titulo               = i.Titulo,
        Descricao            = i.Descricao,
        ViagemId             = i.ViagemId,
        ViagemNumero         = i.Viagem?.NumeroViagem,
        VeiculoId            = i.VeiculoId,
        VeiculoMatricula     = i.Veiculo?.Matricula,
        ClienteId            = i.ClienteId,
        ClienteNome          = i.Cliente?.Nome,
        AtribuicaoId         = i.AtribuicaoId,
        AtribuicaoNumero     = i.Atribuicao?.NumeroAtribuicao,
        DataResolucao        = i.DataResolucao,
        Causa                = i.Causa,
        AcaoCorretiva        = i.AcaoCorretiva,
        ResponsavelResolucao = i.ResponsavelResolucao,
        CustoAssociado       = i.CustoAssociado,
        Observacoes          = i.Observacoes,
        TotalAnexos          = i.Anexos.Count,
        Anexos               = i.Anexos.Select(MapAnexoDto).ToList(),  // ← NOVO
        CriadoEm             = i.CriadoEm,
        AtualizadoEm         = i.AtualizadoEm,
    };
}