using Accusoft.Api.Data;
using Accusoft.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var etiquetas = new List<object>();

        for (int i = 0; i < request.Quantidade; i++)
        {
            var codigo = GerarCodigo(request.Tipo, request.EntidadeId, i);
            etiquetas.Add(new
            {
                id = Guid.NewGuid().ToString(),
                codigo = codigo,
                tipo = request.Tipo,
                dados = new { entidadeId = request.EntidadeId, sequencial = i + 1 }
            });
        }

        return Ok(new { etiquetas, pdfUrl = (string?)null });
    }

    private string GerarCodigo(string tipo, int entidadeId, int sequencial)
    {
        return $"{tipo.ToUpper()[..3]}{entidadeId:D6}{(sequencial + 1):D3}";
    }
}

public record GerarEtiquetaRequest(
    string Tipo,
    int EntidadeId,
    int Quantidade,
    string Formato,
    string Template,
    bool IncluirTexto,
    bool IncluirLogo
);