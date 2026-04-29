using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Accusoft.Api.Controllers;
public record ArmazemResponseDto(
    int     Id,
    string  Codigo,
    string  Nome,
    string? Tipo,
    string? Morada,
    string? Localidade,
    string? CodigoPostal,
    string? Pais,
    string? Telefone,
    string? Email,
    string? ResponsavelNome,
    string? ResponsavelTelefone,
    string? Observacoes,
    bool    Ativo,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm
);

public class ArmazemCreateDto
{
    [Required(ErrorMessage = "Código do armazém é obrigatório.")]
    [MaxLength(50, ErrorMessage = "Código não pode exceder 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nome do armazém é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Tipo { get; set; }

    [MaxLength(300)]
    public string? Morada { get; set; }

    [MaxLength(100)]
    public string? Localidade { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(100)]
    public string? Pais { get; set; } = "Portugal";

    [MaxLength(30)]
    public string? Telefone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Email de contacto inválido.")]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? ResponsavelNome { get; set; }

    [MaxLength(30)]
    public string? ResponsavelTelefone { get; set; }

    public string? Observacoes { get; set; }
}

public class ArmazemUpdateDto : ArmazemCreateDto
{
    public bool Ativo { get; set; } = true;
}

[ApiController]
[Route("api/user/armazens")]
[Authorize]
public class ArmazensController : ControllerBase
{
    private readonly AppDbContext _db;

    public ArmazensController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetArmazens(
        [FromQuery] string? search,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string  orderBy  = "nome",
        [FromQuery] string  orderDir = "asc")
    {
        // ── Multitenancy note ─────────────────────────────────────────────
        // Actualmente filtra por CriadoPor == uid (isolamento por utilizador).
        // Para Shared Ownership (ex: todos da mesma organização vêem os mesmos
        // armazéns), adicionar coluna OrganizacaoId à tabela armazens e filtrar:
        //   .Where(a => a.OrganizacaoId == currentUser.OrganizacaoId)
        // ─────────────────────────────────────────────────────────────────
        var uid = User.GetUserId();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.ArmazensCatalogo
            .AsNoTracking()
            .Where(a => a.CriadoPor == uid);
            

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(a =>
                a.Nome.ToLower().Contains(s)   ||
                a.Codigo.ToLower().Contains(s) ||
                (a.Localidade != null && a.Localidade.ToLower().Contains(s)));
        }

        if (ativo.HasValue)
            query = query.Where(a => a.Ativo == ativo.Value);

        var desc = orderDir.ToLower() == "desc";
        query = orderBy.ToLower() switch
        {
            "codigo"     => desc ? query.OrderByDescending(a => a.Codigo)     : query.OrderBy(a => a.Codigo),
            "localidade" => desc ? query.OrderByDescending(a => a.Localidade) : query.OrderBy(a => a.Localidade),
            "tipo"       => desc ? query.OrderByDescending(a => a.Tipo)       : query.OrderBy(a => a.Tipo),
            _            => desc ? query.OrderByDescending(a => a.Nome)       : query.OrderBy(a => a.Nome),
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<ArmazemResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetArmazem(int id)
    {
        var uid = User.GetUserId();
        var a   = await _db.ArmazensCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        return a is null
            ? NotFound(new { message = "Armazém não encontrado." })
            : Ok(MapToDto(a));
    }

    [HttpPost]
    public async Task<IActionResult> CreateArmazem([FromBody] ArmazemCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid      = User.GetUserId();
        var codigoNorm = dto.Codigo.Trim().ToUpperInvariant();

        if (await _db.ArmazensCatalogo.AnyAsync(a =>
            a.Codigo == codigoNorm && a.CriadoPor == uid))
            return Conflict(new { message = $"Já existe um armazém com o código '{codigoNorm}'." });

        var now = DateTimeOffset.UtcNow;
        var armazem = new Armazem
        {
            Codigo               = codigoNorm,
            Nome                 = dto.Nome.Trim(),
            Tipo                 = dto.Tipo?.Trim(),
            Morada               = dto.Morada?.Trim(),
            Localidade           = dto.Localidade?.Trim(),
            CodigoPostal         = dto.CodigoPostal?.Trim(),
            Pais                 = dto.Pais?.Trim() ?? "Portugal",
            Telefone             = dto.Telefone?.Trim(),
            Email                = dto.Email?.Trim().ToLower(),
            ResponsavelNome      = dto.ResponsavelNome?.Trim(),
            ResponsavelTelefone  = dto.ResponsavelTelefone?.Trim(),
            Observacoes          = dto.Observacoes?.Trim(),
            Ativo                = true,
            CriadoPor            = uid,
            CriadoEm             = now,
            AtualizadoEm         = now,
        };

        _db.ArmazensCatalogo.Add(armazem);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetArmazem), new { id = armazem.Id }, MapToDto(armazem));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateArmazem(int id, [FromBody] ArmazemUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid     = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo
            .FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        var codigoNorm = dto.Codigo.Trim().ToUpperInvariant();

        if (armazem.Codigo != codigoNorm &&
            await _db.ArmazensCatalogo.AnyAsync(a =>
                a.Codigo == codigoNorm && a.CriadoPor == uid && a.Id != id))
            return Conflict(new { message = $"Já existe outro armazém com o código '{codigoNorm}'." });

        armazem.Codigo               = codigoNorm;
        armazem.Nome                 = dto.Nome.Trim();
        armazem.Tipo                 = dto.Tipo?.Trim();
        armazem.Morada               = dto.Morada?.Trim();
        armazem.Localidade           = dto.Localidade?.Trim();
        armazem.CodigoPostal         = dto.CodigoPostal?.Trim();
        armazem.Pais                 = dto.Pais?.Trim() ?? "Portugal";
        armazem.Telefone             = dto.Telefone?.Trim();
        armazem.Email                = dto.Email?.Trim().ToLower();
        armazem.ResponsavelNome      = dto.ResponsavelNome?.Trim();
        armazem.ResponsavelTelefone  = dto.ResponsavelTelefone?.Trim();
        armazem.Observacoes          = dto.Observacoes?.Trim();
        armazem.Ativo                = dto.Ativo;
        armazem.AtualizadoEm         = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(armazem));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteArmazem(int id)
    {
        var uid     = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo
            .FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        armazem.Ativo        = false;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarArmazem(int id)
    {
        var uid     = User.GetUserId();
        var armazem = await _db.ArmazensCatalogo
            .FirstOrDefaultAsync(a => a.Id == id && a.CriadoPor == uid);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        armazem.Ativo        = true;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém ativado com sucesso." });
    }

    private static ArmazemResponseDto MapToDto(Armazem a) => new(
        a.Id, a.Codigo, a.Nome, a.Tipo,
        a.Morada, a.Localidade, a.CodigoPostal, a.Pais,
        a.Telefone, a.Email,
        a.ResponsavelNome, a.ResponsavelTelefone,
        a.Observacoes, a.Ativo,
        a.CriadoEm, a.AtualizadoEm
    );
}
