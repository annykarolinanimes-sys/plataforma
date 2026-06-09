using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Accusoft.Api.Controllers;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoArmazem
{
    principal,
    secundario,
    deposito,
    loja,
    [System.Runtime.Serialization.EnumMember(Value = "cross-dock")]
    cross_dock  
}


public record ArmazemResponseDto(
    int     Id,
    string  Codigo,
    string  Nome,
    string? Tipo,
    string? Morada,
    string? Localizacao,
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
    [Required(ErrorMessage = "Nome do armazém é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    public TipoArmazem? Tipo { get; set; }

    [MaxLength(300)]
    public string? Morada { get; set; }

    [MaxLength(100)]
    public string? Localizacao { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(100)]
    public string? Pais { get; set; } = "Portugal";

    [MaxLength(30)]
    [RegularExpression(
        @"^(\+?[0-9\s\-\(\)]{7,20})$",
        ErrorMessage = "Formato de telefone inválido.")]
    public string? Telefone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Email de contacto inválido.")]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? ResponsavelNome { get; set; }

    [MaxLength(30)]
    [RegularExpression(
        @"^(\+?[0-9\s\-\(\)]{7,20})$",
        ErrorMessage = "Formato de telefone do responsável inválido.")]
    public string? ResponsavelTelefone { get; set; }

    public string? Observacoes { get; set; }
}

public class ArmazemUpdateDto
{
    [MaxLength(50)]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "Nome do armazém é obrigatório.")]
    [MaxLength(200, ErrorMessage = "Nome não pode exceder 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    public TipoArmazem? Tipo { get; set; }

    [MaxLength(300)]
    public string? Morada { get; set; }

    [MaxLength(100)]
    public string? Localizacao { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(100)]
    public string? Pais { get; set; }

    [MaxLength(30)]
    [RegularExpression(
        @"^(\+?[0-9\s\-\(\)]{7,20})$",
        ErrorMessage = "Formato de telefone inválido.")]
    public string? Telefone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Email de contacto inválido.")]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? ResponsavelNome { get; set; }

    [MaxLength(30)]
    [RegularExpression(
        @"^(\+?[0-9\s\-\(\)]{7,20})$",
        ErrorMessage = "Formato de telefone do responsável inválido.")]
    public string? ResponsavelTelefone { get; set; }

    public string? Observacoes { get; set; }

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
        var uid = User.GetUserId();
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.ArmazensCatalogo
            .AsNoTracking();
        if (!User.IsAdmin())
            query = query.Where(a => a.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(a =>
                a.Nome.ToLower().Contains(s)       ||
                a.Codigo.ToLower().Contains(s)     ||
                (a.Localizacao != null && a.Localizacao.ToLower().Contains(s)));
        }

        if (ativo.HasValue)
            query = query.Where(a => a.Ativo == ativo.Value);

        var desc = orderDir.ToLower() == "desc";
        query = orderBy.ToLower() switch
        {
            "codigo"      => desc ? query.OrderByDescending(a => a.Codigo)      : query.OrderBy(a => a.Codigo),
            "localizacao" => desc ? query.OrderByDescending(a => a.Localizacao)  : query.OrderBy(a => a.Localizacao),
            "tipo"        => desc ? query.OrderByDescending(a => a.Tipo)        : query.OrderBy(a => a.Tipo),
            _             => desc ? query.OrderByDescending(a => a.Nome)        : query.OrderBy(a => a.Nome),
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
        var query = _db.ArmazensCatalogo
            .AsNoTracking();
        if (!User.IsAdmin())
            query = query.Where(a => a.CriadoPor == uid);

        var a = await query.FirstOrDefaultAsync(a => a.Id == id);

        return a is null
            ? NotFound(new { message = "Armazém não encontrado." })
            : Ok(MapToDto(a));
    }

    [HttpPost]
    public async Task<IActionResult> CreateArmazem([FromBody] ArmazemCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { message = "Erro de validação.", errors });
        }

        var uid          = User.GetUserId();
        var nextNumber   = await GetNextArmazemNumber(uid);
        var codigoGerado = $"ARM-{nextNumber:D2}";
        var now          = DateTimeOffset.UtcNow;

        var tipoStr = dto.Tipo.HasValue
            ? dto.Tipo.Value.ToString().Replace('_', '-')
            : "principal";

        var armazem = new Armazem
        {
            Codigo               = codigoGerado,
            Nome                 = dto.Nome.Trim(),
            Tipo                 = tipoStr,
            Morada               = dto.Morada?.Trim(),
            Localizacao          = dto.Localizacao?.Trim(),    
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

        try
        {
            _db.ArmazensCatalogo.Add(armazem);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Erro ao guardar: {ex.Message}" });
        }

        return CreatedAtAction(nameof(GetArmazem), new { id = armazem.Id }, MapToDto(armazem));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateArmazem(int id, [FromBody] ArmazemUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();
        var query = _db.ArmazensCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(a => a.CriadoPor == uid);

        var armazem = await query.FirstOrDefaultAsync(a => a.Id == id);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        var tipoStr = dto.Tipo.HasValue
            ? dto.Tipo.Value.ToString().Replace('_', '-')
            : armazem.Tipo;

        armazem.Nome                = dto.Nome.Trim();
        armazem.Tipo                = tipoStr;
        armazem.Morada              = dto.Morada?.Trim();
        armazem.Localizacao         = dto.Localizacao?.Trim();    
        armazem.CodigoPostal        = dto.CodigoPostal?.Trim();
        armazem.Pais                = dto.Pais?.Trim() ?? "Portugal";
        armazem.Telefone            = dto.Telefone?.Trim();
        armazem.Email               = dto.Email?.Trim().ToLower();
        armazem.ResponsavelNome     = dto.ResponsavelNome?.Trim();
        armazem.ResponsavelTelefone = dto.ResponsavelTelefone?.Trim();
        armazem.Observacoes         = dto.Observacoes?.Trim();
        armazem.Ativo               = dto.Ativo;
        armazem.AtualizadoEm        = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(armazem));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteArmazem(int id)
    {
        var uid = User.GetUserId();
        var query = _db.ArmazensCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(a => a.CriadoPor == uid);

        var armazem = await query.FirstOrDefaultAsync(a => a.Id == id);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        var temMovimentacoesPendentes = await TemMovimentacoesPendentes(id);
        if (temMovimentacoesPendentes)
            return Conflict(new
            {
                message = "Não é possível desativar o armazém: existem movimentações de stock pendentes associadas."
            });

        armazem.Ativo        = false;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém desactivado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarArmazem(int id)
    {
        var uid = User.GetUserId();
        var query = _db.ArmazensCatalogo.AsQueryable();
        if (!User.IsAdmin())
            query = query.Where(a => a.CriadoPor == uid);

        var armazem = await query.FirstOrDefaultAsync(a => a.Id == id);

        if (armazem is null)
            return NotFound(new { message = "Armazém não encontrado." });

        armazem.Ativo        = true;
        armazem.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Armazém activado com sucesso." });
    }


    private async Task<bool> TemMovimentacoesPendentes(int armazemId)
    {
        return await _db.Estoques
            .AsNoTracking()
            .AnyAsync(e =>
                e.ArmazemId == armazemId &&
                (e.Quantidade - e.QuantidadeReservada) > 0);

    }

    private async Task<int> GetNextArmazemNumber(int userId)
    {
        try
        {
            var existingCodes = await _db.ArmazensCatalogo
                .Where(a => a.Codigo != null && a.Codigo.StartsWith("ARM-"))
                .Select(a => a.Codigo)
                .ToListAsync();

            var maxNumber = 0;
            foreach (var code in existingCodes)
            {
                var parts = code.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var num))
                    if (num > maxNumber) maxNumber = num;
            }
            return maxNumber + 1;
        }
        catch { return 1; }
    }

    private static ArmazemResponseDto MapToDto(Armazem a) => new(
        a.Id, a.Codigo, a.Nome, a.Tipo,
        a.Morada, a.Localizacao, a.CodigoPostal, a.Pais,
        a.Telefone, a.Email,
        a.ResponsavelNome, a.ResponsavelTelefone,
        a.Observacoes, a.Ativo,
        a.CriadoEm, a.AtualizadoEm
    );
}
