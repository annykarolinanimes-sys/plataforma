using Accusoft.Api.Data;
using Accusoft.Api.DTOs;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/clientes-catalogo")]
[Authorize]
public class ClientesCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientesCatalogoController(AppDbContext db)
    {
        _db = db;
    }

    // Método auxiliar para gerar código único
    private async Task<string> GerarCodigoUnicoAsync(int userId)
    {
        var prefix = "CLI";
        var lastNumber = 0;
        
        // Buscar o último código criado pelo usuário
        var ultimoCodigo = await _db.ClientesCatalogo
            .Where(c => c.CriadoPor == userId && c.Codigo.StartsWith(prefix))
            .OrderByDescending(c => c.Id)
            .Select(c => c.Codigo)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(ultimoCodigo))
        {
            var parts = ultimoCodigo.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var num))
            {
                lastNumber = num;
            }
        }

        var novoNumero = (lastNumber + 1).ToString("D4");
        return $"{prefix}-{novoNumero}";
    }

    [HttpGet]
    public async Task<IActionResult> GetClientes(
        [FromQuery] string? search,
        [FromQuery] bool?   ativo,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var uid = User.GetUserId();

        var query = _db.ClientesCatalogo
            .AsNoTracking()
            .Where(c => c.CriadoPor == uid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.Nome.ToLower().Contains(s) ||
                c.Codigo.ToLower().Contains(s) ||
                (c.Contribuinte != null && c.Contribuinte.Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        if (ativo.HasValue)
            query = query.Where(c => c.Ativo == ativo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<ClienteResponseDto>
        {
            Items    = items.Select(MapToDto).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCliente(int id)
    {
        var uid     = User.GetUserId();
        var cliente = await _db.ClientesCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.CriadoPor == uid);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        return Ok(MapToDto(cliente));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCliente([FromBody] ClienteCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid = User.GetUserId();

        // Se o código foi fornecido, validar unicidade
        if (!string.IsNullOrWhiteSpace(dto.Codigo))
        {
            if (await _db.ClientesCatalogo.AnyAsync(c =>
                    c.Codigo == dto.Codigo.Trim() && c.CriadoPor == uid))
                return Conflict(new { message = "Já existe um cliente com este código." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Contribuinte) &&
            await _db.ClientesCatalogo.AnyAsync(c =>
                c.Contribuinte == dto.Contribuinte.Trim() && c.CriadoPor == uid))
            return Conflict(new { message = "Já existe um cliente com este contribuinte (NIF)." });

        var now = DateTimeOffset.UtcNow;
        
        // Gerar código automaticamente se não foi fornecido
        var codigo = string.IsNullOrWhiteSpace(dto.Codigo) 
            ? await GerarCodigoUnicoAsync(uid) 
            : dto.Codigo.Trim();

        var cliente = new ClienteCatalogo
        {
            Codigo            = codigo,
            Nome              = dto.Nome.Trim(),
            Contribuinte      = dto.Contribuinte?.Trim(),
            Telefone          = dto.Telefone?.Trim(),
            Email             = dto.Email?.Trim().ToLower(),
            Morada            = dto.Morada?.Trim(),
            Localidade        = dto.Localidade?.Trim(),
            CodigoPostal      = dto.CodigoPostal?.Trim(),
            Pais              = dto.Pais?.Trim() ?? "Portugal",
            ContactoNome      = dto.ContactoNome?.Trim(),
            ContactoTelefone  = dto.ContactoTelefone?.Trim(),
            Observacoes       = dto.Observacoes?.Trim(),
            Ativo             = true,
            CriadoPor         = uid,
            CriadoEm          = now,
            AtualizadoEm      = now
        };

        _db.ClientesCatalogo.Add(cliente);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, MapToDto(cliente));
    }
    [HttpGet("proximo-codigo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProximoCodigo()
    {
        int uid;
        try
        {
            uid = User.GetUserId();
        }
        catch
        {
            uid = 0; // Para usuários não autenticados, usar uid 0 para preview
        }
        var proximoCodigo = await GerarCodigoUnicoAsync(uid);
        return Ok(new { codigo = proximoCodigo });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCliente(int id, [FromBody] ClienteUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var uid     = User.GetUserId();
        var cliente = await _db.ClientesCatalogo
            .FirstOrDefaultAsync(c => c.Id == id && c.CriadoPor == uid);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        if (cliente.Codigo != dto.Codigo.Trim() &&
            await _db.ClientesCatalogo.AnyAsync(c =>
                c.Codigo == dto.Codigo.Trim() && c.CriadoPor == uid && c.Id != id))
            return Conflict(new { message = "Já existe outro cliente com este código." });

        if (!string.IsNullOrWhiteSpace(dto.Contribuinte) &&
            cliente.Contribuinte != dto.Contribuinte.Trim() &&
            await _db.ClientesCatalogo.AnyAsync(c =>
                c.Contribuinte == dto.Contribuinte.Trim() && c.CriadoPor == uid && c.Id != id))
            return Conflict(new { message = "Já existe outro cliente com este contribuinte (NIF)." });

        cliente.Codigo           = dto.Codigo.Trim();
        cliente.Nome             = dto.Nome.Trim();
        cliente.Contribuinte     = dto.Contribuinte?.Trim();
        cliente.Telefone         = dto.Telefone?.Trim();
        cliente.Email            = dto.Email?.Trim().ToLower();
        cliente.Morada           = dto.Morada?.Trim();
        cliente.Localidade       = dto.Localidade?.Trim();
        cliente.CodigoPostal     = dto.CodigoPostal?.Trim();
        cliente.Pais             = dto.Pais?.Trim() ?? "Portugal";
        cliente.ContactoNome     = dto.ContactoNome?.Trim();
        cliente.ContactoTelefone = dto.ContactoTelefone?.Trim();
        cliente.Observacoes      = dto.Observacoes?.Trim();
        cliente.Ativo            = dto.Ativo;
        cliente.AtualizadoEm     = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(cliente));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var uid     = User.GetUserId();
        var cliente = await _db.ClientesCatalogo
            .FirstOrDefaultAsync(c => c.Id == id && c.CriadoPor == uid);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        cliente.Ativo        = false;
        cliente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Cliente desativado com sucesso." });
    }

    [HttpPost("{id:int}/ativar")]
    public async Task<IActionResult> AtivarCliente(int id)
    {
        var uid     = User.GetUserId();
        var cliente = await _db.ClientesCatalogo
            .FirstOrDefaultAsync(c => c.Id == id && c.CriadoPor == uid);

        if (cliente is null)
            return NotFound(new { message = "Cliente não encontrado." });

        cliente.Ativo        = true;
        cliente.AtualizadoEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Cliente ativado com sucesso." });
    }

    private static ClienteResponseDto MapToDto(ClienteCatalogo c) => new()
    {
        Id                = c.Id,
        Codigo            = c.Codigo,
        Nome              = c.Nome,
        Contribuinte      = c.Contribuinte,
        Telefone          = c.Telefone,
        Email             = c.Email,
        Morada            = c.Morada,
        Localidade        = c.Localidade,
        CodigoPostal      = c.CodigoPostal,
        Pais              = c.Pais,
        ContactoNome      = c.ContactoNome,
        ContactoTelefone  = c.ContactoTelefone,
        Observacoes       = c.Observacoes,
        Ativo             = c.Ativo,
        CriadoEm          = c.CriadoEm,
        AtualizadoEm      = c.AtualizadoEm
    };
}
