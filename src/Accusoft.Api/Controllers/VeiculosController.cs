using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Accusoft.Api.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record VeiculoResponseDto(
    int      Id,
    string   Matricula,
    string   Marca,
    string   Modelo,
    string?  Cor,
    int?     Ano,
    string?  Vin,
    string?  TipoCombustivel,
    int?     Cilindrada,
    int?     Potencia,
    int?     Lugares,
    decimal? Peso,
    int?     ProprietarioId,
    string?  ProprietarioNome,
    string?  ProprietarioCodigo,
    bool     Ativo,
    string?  Observacoes,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    List<VeiculoAnexoDto> Anexos
);

public record VeiculoAnexoDto(
    int Id,
    string NomeOriginal,
    string MimeType,
    long TamanhoBytes,
    string TamanhoFormatado,
    DateTimeOffset CriadoEm
);

public record VeiculoCreateDto(
    string   Matricula,
    string   Marca,
    string   Modelo,
    string?  Cor,
    int?     Ano,
    string?  Vin,
    string?  TipoCombustivel,
    int?     Cilindrada,
    int?     Potencia,
    int?     Lugares,
    decimal? Peso,
    int?     ProprietarioId,
    string?  Observacoes
);

public record VeiculoUpdateDto(
    string   Matricula,
    string   Marca,
    string   Modelo,
    string?  Cor,
    int?     Ano,
    string?  Vin,
    string?  TipoCombustivel,
    int?     Cilindrada,
    int?     Potencia,
    int?     Lugares,
    decimal? Peso,
    int?     ProprietarioId,
    string?  Observacoes,
    bool     Ativo
);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/user/veiculos")]
[Authorize]
public class VeiculosController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly IFileStorageService _fileStorage;

    // Allowed MIME types for the supporting document upload
    private static readonly HashSet<string> AllowedDocMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedAnexoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> AllowedAnexoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxAnexoSizeBytes    = 10 * 1024 * 1024; // 10 MB

    public VeiculosController(AppDbContext db, IFileStorageService fileStorage)
    {
        _db          = db;
        _fileStorage = fileStorage;
    }

    // ── GET /api/user/veiculos/debug/info ────────────────────────────────────

    [HttpGet("debug/info")]
    public async Task<IActionResult> DebugInfo()
    {
        var uid = User.GetUserId();
        var isAdmin = User.IsAdmin();
        var veiculos = await _db.Veiculos.ToListAsync();

        return Ok(new
        {
            currentUserId = uid,
            isAdmin = isAdmin,
            totalVeiculos = veiculos.Count,
            veiculos = veiculos.Select(v => new
            {
                v.Id,
                v.Matricula,
                v.Marca,
                v.CriadoPor,
                isMine = v.CriadoPor == uid
            }).ToList()
        });
    }

    // ── GET /api/user/veiculos ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetVeiculos(
        [FromQuery] string? search,
        [FromQuery] string? combustivel,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 15,
        [FromQuery] string  orderBy  = "marca",
        [FromQuery] string  orderDir = "asc")
    {
        var uid = User.GetUserId();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.Veiculos
            .AsNoTracking()
            .Include(v => v.Proprietario)
            .Include(v => v.Anexos)
            .AsQueryable();

        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            var idFiltro = int.TryParse(search.Trim(), out var parsedId) ? parsedId : (int?)null;

            query = query.Where(v =>
                (idFiltro.HasValue && v.Id == idFiltro.Value) ||
                (v.Matricula != null && v.Matricula.ToLower().Contains(s)) ||
                (v.Marca != null && v.Marca.ToLower().Contains(s)) ||
                (v.Modelo != null && v.Modelo.ToLower().Contains(s)) ||
                (v.Vin != null && v.Vin.ToLower().Contains(s)) ||
                (v.Cor != null && v.Cor.ToLower().Contains(s)) ||
                (v.TipoCombustivel != null && v.TipoCombustivel.ToLower().Contains(s)) ||
                (v.Observacoes != null && v.Observacoes.ToLower().Contains(s)) ||
                (v.Proprietario != null && v.Proprietario.Nome != null && v.Proprietario.Nome.ToLower().Contains(s)) ||
                (v.Proprietario != null && v.Proprietario.Codigo != null && v.Proprietario.Codigo.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(combustivel))
            query = query.Where(v =>
                v.TipoCombustivel != null &&
                v.TipoCombustivel.ToLower() == combustivel.ToLower());

        if (ativo.HasValue)
            query = query.Where(v => v.Ativo == ativo.Value);

        var descending = orderDir.Equals("desc", StringComparison.OrdinalIgnoreCase);
        query = orderBy.ToLower() switch
        {
            "matricula" => descending ? query.OrderByDescending(v => v.Matricula) : query.OrderBy(v => v.Matricula),
            "modelo"    => descending ? query.OrderByDescending(v => v.Modelo)    : query.OrderBy(v => v.Modelo),
            "ano"       => descending ? query.OrderByDescending(v => v.Ano)       : query.OrderBy(v => v.Ano),
            _           => descending ? query.OrderByDescending(v => v.Marca)     : query.OrderBy(v => v.Marca),
        };

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


    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetVeiculo(int id)
    {
        var uid   = User.GetUserId();
        var query = _db.Veiculos
            .AsNoTracking()
            .Include(v => v.Proprietario)
            .Include(v => v.Anexos)
            .Where(v => v.Id == id);

        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        var v = await query.FirstOrDefaultAsync();

        return v is null
            ? NotFound(new { message = "Veículo não encontrado." })
            : Ok(MapToDto(v));
    }

    // ── POST /api/user/veiculos ──────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateVeiculo([FromBody] VeiculoCreateDto dto)
    {
        var uid = User.GetUserId();

        if (string.IsNullOrWhiteSpace(dto.Matricula))
            return BadRequest(new { message = "Matrícula é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        // VIN is required for new vehicles
        if (string.IsNullOrWhiteSpace(dto.Vin))
            return BadRequest(new { message = "VIN/Chassis é obrigatório." });

        var vinNorm = dto.Vin.Trim().ToUpperInvariant();

        var vinError = ValidateVin(vinNorm);
        if (vinError is not null)
            return BadRequest(new { message = vinError });

        // VIN uniqueness check
        if (await _db.Veiculos.AnyAsync(v => v.Vin == vinNorm))
            return Conflict(new { message = $"Já existe um veículo com o VIN '{vinNorm}'." });

        var matriculaNorm = dto.Matricula.Trim().ToUpperInvariant();
        if (await _db.Veiculos.AnyAsync(v => v.Matricula == matriculaNorm))
            return Conflict(new { message = $"Já existe um veículo com a matrícula '{matriculaNorm}'." });

        var now = DateTimeOffset.UtcNow;
        var veiculo = new Veiculo
        {
            Matricula        = matriculaNorm,
            Marca            = dto.Marca.Trim(),
            Modelo           = dto.Modelo.Trim(),
            Cor              = dto.Cor?.Trim(),
            Ano              = dto.Ano,
            Vin              = vinNorm,
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
        
        // Load related entities for response
        await _db.Entry(veiculo).Reference(v => v.Proprietario).LoadAsync();
        await _db.Entry(veiculo).Collection(v => v.Anexos).LoadAsync();

        return CreatedAtAction(nameof(GetVeiculo), new { id = veiculo.Id }, MapToDto(veiculo));
    }

    // ── PUT /api/user/veiculos/{id} ──────────────────────────────────────────

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateVeiculo(int id, [FromBody] VeiculoUpdateDto dto)
    {
        var uid   = User.GetUserId();
        var query = _db.Veiculos
            .Include(v => v.Proprietario)
            .Where(v => v.Id == id);

        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        var veiculo = await query.FirstOrDefaultAsync();
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        if (string.IsNullOrWhiteSpace(dto.Marca))
            return BadRequest(new { message = "Marca é obrigatória." });
        if (string.IsNullOrWhiteSpace(dto.Modelo))
            return BadRequest(new { message = "Modelo é obrigatório." });

        // VIN validation (required in PUT as well)
        if (string.IsNullOrWhiteSpace(dto.Vin))
            return BadRequest(new { message = "VIN/Chassis é obrigatório." });

        var vinNorm = dto.Vin.Trim().ToUpperInvariant();
        var vinError = ValidateVin(vinNorm);
        if (vinError is not null)
            return BadRequest(new { message = vinError });

        // VIN uniqueness check (excluding self)
        if (veiculo.Vin != vinNorm && await _db.Veiculos.AnyAsync(v => v.Vin == vinNorm && v.Id != id))
            return Conflict(new { message = $"Já existe outro veículo com o VIN '{vinNorm}'." });

        // Matricula is NOT updated via this endpoint — use POST /{id}/change-matricula
        // We silently preserve the existing one even if the client sends a different value.
        veiculo.Marca           = dto.Marca.Trim();
        veiculo.Modelo          = dto.Modelo.Trim();
        veiculo.Cor             = dto.Cor?.Trim();
        veiculo.Ano             = dto.Ano;
        veiculo.Vin             = vinNorm;
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

    // ── DELETE /api/user/veiculos/{id} ───────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVeiculo(int id)
    {
        var uid   = User.GetUserId();
        var query = _db.Veiculos.Where(v => v.Id == id);
        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        var veiculo = await query.FirstOrDefaultAsync();
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo        = false;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo desativado com sucesso." });
    }

    // ── POST /api/user/veiculos/{id}/ativar ──────────────────────────────────

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarVeiculo(int id)
    {
        var uid   = User.GetUserId();
        var query = _db.Veiculos.Where(v => v.Id == id);
        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        var veiculo = await query.FirstOrDefaultAsync();
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        veiculo.Ativo        = true;
        veiculo.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Veículo ativado com sucesso." });
    }

    // ── POST /api/user/veiculos/{id}/change-matricula ────────────────────────
    /// <summary>
    /// Altera a matrícula de um veículo. Exige motivo (texto) e documento
    /// comprovativo (ficheiro). A operação cria um registo de anexo associado.
    /// </summary>
    [HttpPost("{id:int}/change-matricula")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ChangeMatricula(
        int id,
        [FromForm] string  novaMatricula,
        [FromForm] string  motivo,
        [FromForm] IFormFile documento)
    {
        var uid = User.GetUserId();

        // ── 1. Load the vehicle ──────────────────────────────────────────────
        var query = _db.Veiculos.Where(v => v.Id == id);
        if (!User.IsAdmin())
            query = query.Where(v => v.CriadoPor == uid);

        var veiculo = await query.FirstOrDefaultAsync();
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        // ── 2. Validate new matricula ────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(novaMatricula))
            return BadRequest(new { message = "Nova matrícula é obrigatória." });

        var matriculaNorm = novaMatricula.Trim().ToUpperInvariant();

        // Format validation (Portuguese plates: AA-00-AA | 00-AA-00 | 00-00-AA)
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            matriculaNorm, @"^([A-Z]{2}-\d{2}-[A-Z]{2}|\d{2}-[A-Z]{2}-\d{2}|\d{2}-\d{2}-[A-Z]{2})$"))
        {
            return BadRequest(new { message = "Formato de matrícula inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA." });
        }

        if (matriculaNorm == veiculo.Matricula)
            return BadRequest(new { message = "A nova matrícula deve ser diferente da atual." });

        // Uniqueness check
        if (await _db.Veiculos.AnyAsync(v => v.Matricula == matriculaNorm && v.Id != id))
            return Conflict(new { message = $"Já existe um veículo com a matrícula '{matriculaNorm}'." });

        // ── 3. Validate motivo ───────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
            return BadRequest(new { message = "O motivo da troca deve ter pelo menos 10 caracteres." });

        // ── 4. Validate and save the document ────────────────────────────────
        if (documento is null || documento.Length == 0)
            return BadRequest(new { message = "O documento comprovativo é obrigatório." });

        if (documento.Length > MaxDocumentSizeBytes)
            return BadRequest(new { message = "O documento não pode exceder 10 MB." });

        if (!AllowedDocMimeTypes.Contains(documento.ContentType))
            return BadRequest(new { message = "Apenas PDF, JPEG, PNG ou WebP são aceites como documento comprovativo." });

        // Save file using the shared storage service
        var (pathUrl, tamanhoBytes, _) = await _fileStorage.SaveAsync(documento, uid);

        // ── 5. Persist changes ───────────────────────────────────────────────
        var matriculaAnterior = veiculo.Matricula;
        veiculo.Matricula     = matriculaNorm;
        veiculo.AtualizadoEm  = DateTimeOffset.UtcNow;
        // Append the reason to existing observations so there's a clear audit trail
        var notaAudit = $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] Troca de matrícula: {matriculaAnterior} → {matriculaNorm}. Motivo: {motivo.Trim()}";
        veiculo.Observacoes   = (veiculo.Observacoes ?? "") + notaAudit;

        // Create the VeiculoAnexo record for the supporting document
        var anexo = new VeiculoAnexo
        {
            VeiculoId     = veiculo.Id,
            NomeOriginal  = documento.FileName,
            NomeFicheiro  = System.IO.Path.GetFileName(pathUrl),
            PathUrl       = pathUrl,
            MimeType      = documento.ContentType,
            TamanhoBytes  = tamanhoBytes,
            UsuarioId     = uid,
            CriadoEm     = DateTimeOffset.UtcNow,
        };

        _db.VeiculoAnexos.Add(anexo);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message        = $"Matrícula alterada com sucesso de '{matriculaAnterior}' para '{matriculaNorm}'.",
            novaMatricula  = matriculaNorm,
            anexoId        = anexo.Id,
        });
    }

    /// <summary>
    /// POST /api/user/veiculos/{id}/anexos
    /// Faz upload de um ficheiro e associa-o ao veículo.
    /// </summary>
    [HttpPost("{id:int}/anexos")]
    [RequestSizeLimit(10 * 1024 * 1024 + 1024)]
    public async Task<IActionResult> UploadAnexo(int id, [FromForm] IFormFile ficheiro)
    {
        if (ficheiro is null || ficheiro.Length == 0)
            return BadRequest(new { message = "Ficheiro é obrigatório." });

        if (ficheiro.Length > MaxAnexoSizeBytes)
            return BadRequest(new { message = "O ficheiro não pode exceder 10 MB." });

        var ext = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!AllowedAnexoExtensions.Contains(ext))
            return BadRequest(new { message = "Extensão de ficheiro não permitida." });

        if (!AllowedAnexoMimeTypes.Contains(ficheiro.ContentType))
            return BadRequest(new { message = "Apenas ficheiros PDF, JPEG, PNG ou WebP são permitidos." });

        var uid = User.GetUserId();

        // Verifica se o veículo existe
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id);
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        // Verifica se o utilizador tem permissão para fazer upload
        if (!User.IsAdmin() && veiculo.CriadoPor != uid)
            return Forbid(); // 403 Forbidden

        var (pathUrl, tamanhoBytes, _) = await _fileStorage.SaveAsync(ficheiro, uid);

        var anexo = new VeiculoAnexo
        {
            VeiculoId     = id,
            NomeOriginal  = Path.GetFileName(ficheiro.FileName),
            NomeFicheiro  = Path.GetFileName(pathUrl),
            PathUrl       = pathUrl,
            MimeType      = ficheiro.ContentType,
            TamanhoBytes  = tamanhoBytes,
            UsuarioId     = uid,
            CriadoEm      = DateTimeOffset.UtcNow
        };

        _db.VeiculoAnexos.Add(anexo);
        await _db.SaveChangesAsync();

        return Ok(MapAnexoDto(anexo));
    }

    /// <summary>
    /// GET /api/user/veiculos/{id}/anexos/{anexoId}
    /// Faz download de um anexo do veículo.
    /// </summary>
    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> DownloadAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        // Verifica se o veículo existe
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id);
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        // Verifica se o utilizador tem permissão para fazer download
        if (!User.IsAdmin() && veiculo.CriadoPor != uid)
            return Forbid(); // 403 Forbidden

        var anexo = await _db.VeiculoAnexos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.VeiculoId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        var stream = _fileStorage.GetStream(anexo.PathUrl);
        if (stream is null)
            return NotFound(new { message = "Ficheiro não encontrado no servidor." });

        return File(stream, anexo.MimeType, anexo.NomeOriginal);
    }

    /// <summary>
    /// DELETE /api/user/veiculos/{id}/anexos/{anexoId}
    /// Remove um anexo do veículo.
    /// </summary>
    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> RemoverAnexo(int id, int anexoId)
    {
        var uid = User.GetUserId();

        // Verifica se o veículo existe
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == id);
        if (veiculo is null)
            return NotFound(new { message = "Veículo não encontrado." });

        // Verifica se o utilizador tem permissão para remover
        if (!User.IsAdmin() && veiculo.CriadoPor != uid)
            return Forbid(); // 403 Forbidden

        var anexo = await _db.VeiculoAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.VeiculoId == id);

        if (anexo is null)
            return NotFound(new { message = "Anexo não encontrado." });

        try { _fileStorage.Delete(anexo.PathUrl); }
        catch { }

        _db.VeiculoAnexos.Remove(anexo);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Anexo removido com sucesso." });
    }


    private static string? ValidateVin(string vin)
    {
        if (vin.Length != 17)
            return $"O VIN deve ter exactamente 17 caracteres (recebido: {vin.Length}).";

        if (vin.IndexOfAny(new[] { 'I', 'O', 'Q' }) >= 0)
            return "O VIN não pode conter as letras I, O ou Q.";

        if (!System.Text.RegularExpressions.Regex.IsMatch(vin, @"^[A-HJ-NPR-Z0-9]{17}$"))
            return "O VIN contém caracteres inválidos. Use apenas letras e números (exceto I, O, Q).";

        var transliteration = new Dictionary<char, int>
        {
            {'A',1},{'B',2},{'C',3},{'D',4},{'E',5},{'F',6},{'G',7},{'H',8},
            {'J',1},{'K',2},{'L',3},{'M',4},{'N',5},        {'P',7},{'R',9},
                    {'S',2},{'T',3},{'U',4},{'V',5},{'W',6},{'X',7},{'Y',8},{'Z',9},
            {'0',0},{'1',1},{'2',2},{'3',3},{'4',4},{'5',5},{'6',6},{'7',7},{'8',8},{'9',9},
        };
        int[] weights = { 8,7,6,5,4,3,2,10,0,9,8,7,6,5,4,3,2 };

        int sum = 0;
        for (int i = 0; i < 17; i++)
        {
            if (!transliteration.TryGetValue(vin[i], out var val))
                return "O VIN contém caracteres inválidos.";
            sum += val * weights[i];
        }

        var remainder   = sum % 11;
        var checkDigit  = remainder == 10 ? 'X' : (char)('0' + remainder);

        if (vin[8] != checkDigit)
            return $"Dígito de controlo do VIN inválido (posição 9). Esperado: '{checkDigit}', recebido: '{vin[8]}'.";

        return null; 
    }

    private static VeiculoResponseDto MapToDto(Veiculo v) => new(
        v.Id, v.Matricula, v.Marca, v.Modelo,
        v.Cor, v.Ano, v.Vin, v.TipoCombustivel,
        v.Cilindrada, v.Potencia, v.Lugares, v.Peso,
        v.ProprietarioId,
        v.Proprietario?.Nome,
        v.Proprietario?.Codigo,
        v.Ativo, v.Observacoes,
        v.CriadoEm, v.AtualizadoEm,
        v.Anexos.Select(MapAnexoDto).ToList()
    );

    private static VeiculoAnexoDto MapAnexoDto(VeiculoAnexo a) => new(
        a.Id,
        a.NomeOriginal,
        a.MimeType,
        a.TamanhoBytes,
        FormatFileSize(a.TamanhoBytes),
        a.CriadoEm
    );

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
    }
}


public class PagedResult<T>
{
    public List<T> Items    { get; set; } = new();
    public int     Total    { get; set; }
    public int     Page     { get; set; }
    public int     PageSize { get; set; }
    public int     TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}