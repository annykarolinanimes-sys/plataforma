using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Accusoft.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Controllers;

[ApiController]
[Route("api/user/etiquetas")]
[Authorize]
public class EtiquetasController : ControllerBase
{
    private readonly AppDbContext _db;

    public EtiquetasController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("gerar")]
    public async Task<IActionResult> GerarEtiquetas([FromBody] GerarEtiquetaRequest request)
    {
        var uid = User.GetUserId();
        
        if (request.Quantidade <= 0 || request.Quantidade > 500)
            return BadRequest("Quantidade inválida (máximo 500).");

        string infoAdicional = "";
        if (request.Tipo.ToLower() == "produto")
        {
            var produto = await _db.Produtos.FirstOrDefaultAsync(p => p.Id == request.EntidadeId && p.CriadoPor == uid);
            infoAdicional = produto?.Sku ?? "";
        }
        else if (request.Tipo.ToLower() == "recepcao")
        {
            var recepcao = await _db.Recepcoes.FirstOrDefaultAsync(r => r.Id == request.EntidadeId && r.UsuarioId == uid);
            infoAdicional = recepcao?.NumeroRecepcao ?? "";
        }

        var etiquetas = new List<object>();
        for (int i = 0; i < request.Quantidade; i++)
        {
            var codigo = GerarCodigo(request.Tipo, request.EntidadeId, i, infoAdicional);
            etiquetas.Add(new
            {
                id = Guid.NewGuid().ToString(),
                codigo = codigo,
                tipo = request.Tipo,
                entidadeId = request.EntidadeId,
                sequencial = i + 1,
                dataGeracao = DateTime.UtcNow
            });
        }

        return Ok(new { 
            etiquetas, 
            totalGerado = etiquetas.Count,
            infoEntidade = infoAdicional 
        });
    }

    private string GerarCodigo(string tipo, int entidadeId, int sequencial, string info)
    {
        string prefixo = tipo.Length >= 3 ? tipo.ToUpper()[..3] : tipo.ToUpper();
        return $"{prefixo}-{entidadeId:D6}-{(sequencial + 1):D3}";
    }
}

public class GerarEtiquetaRequest
{
    public string Tipo { get; set; } = string.Empty; 
    public int EntidadeId { get; set; }
    public int Quantidade { get; set; }
    public string? Template { get; set; }
}