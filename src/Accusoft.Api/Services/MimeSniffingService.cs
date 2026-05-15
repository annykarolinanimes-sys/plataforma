using Microsoft.Extensions.Logging;
namespace Accusoft.Api.Services;

public interface IMimeSniffingService
{
    /// <summary>
    /// Analisa os magic bytes da stream para determinar o MIME type real.
    /// Nunca confia na extensão do ficheiro nem no Content-Type do cliente.
    /// </summary>
    Task<ResultadoMimeSniff> AnalisarAsync(Stream stream, string nomeOriginal, CancellationToken ct = default);
}

public sealed record ResultadoMimeSniff(
    bool Permitido,
    string MimeTypeDetectado,
    string? MimeTypeDeclarado,   // Extensão que o utilizador alegou
    bool MimeConflito,           // true = extensão não bate com magic bytes
    string? MotivoBloqueo
);

// ═════════════════════════════════════════════════════════════════════════════
// Implementação
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Deteta o tipo real de um ficheiro via análise de Magic Numbers (file signatures).
///
/// ATAQUE BLOQUEADO: ficheiro malicioso renomeado.
///   Ex: exploit.exe renomeado para relatorio.pdf
///   Ex: script.js renomeado para foto.jpg
///
/// Pipeline:
///   1. Ler os primeiros N bytes da stream
///   2. Comparar com tabela de magic bytes conhecida
///   3. Verificar se o MIME real coincide com a extensão declarada
///   4. Bloquear ficheiros executáveis, scripts e tipos não autorizados
/// </summary>
public sealed class MimeSniffingService : IMimeSniffingService
{
    private readonly ILogger<MimeSniffingService> _logger;

    // Bytes a ler para análise — suficiente para todos os headers conhecidos
    private const int MagicBytesLength = 262;

    public MimeSniffingService(ILogger<MimeSniffingService> logger)
        => _logger = logger;

    // ─── Tabela de Magic Numbers ──────────────────────────────────────────────

    /// <summary>
    /// Mapeamento de magic bytes → MIME type.
    /// Ordenado por comprimento descendente (matches mais específicos primeiro).
    /// </summary>
    private static readonly IReadOnlyList<MagicSignature> Signatures = new List<MagicSignature>
    {
        // ── Documentos ────────────────────────────────────────────────────────
        new("application/pdf",        [0x25, 0x50, 0x44, 0x46],         Offset: 0),  // %PDF
        new("application/msword",     [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], 0), // OLE2 (DOC, XLS, PPT)
        new("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                      [0x50, 0x4B, 0x03, 0x04], 0),    // ZIP-based (DOCX, XLSX, PPTX)
        new("application/zip",        [0x50, 0x4B, 0x05, 0x06], 0),    // ZIP empty
        new("application/zip",        [0x50, 0x4B, 0x07, 0x08], 0),    // ZIP spanned

        // ── Imagens ───────────────────────────────────────────────────────────
        new("image/jpeg",             [0xFF, 0xD8, 0xFF],               0),
        new("image/png",              [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0),
        new("image/gif",              [0x47, 0x49, 0x46, 0x38, 0x37, 0x61], 0), // GIF87a
        new("image/gif",              [0x47, 0x49, 0x46, 0x38, 0x39, 0x61], 0), // GIF89a
        new("image/webp",             [0x52, 0x49, 0x46, 0x46],         0),     // RIFF (verificar WEBP em offset 8)
        new("image/tiff",             [0x49, 0x49, 0x2A, 0x00],         0),     // TIFF little-endian
        new("image/tiff",             [0x4D, 0x4D, 0x00, 0x2A],         0),     // TIFF big-endian
        new("image/bmp",              [0x42, 0x4D],                      0),

        new("application/x-7z-compressed", [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], 0),
        new("application/x-rar-compressed",[0x52, 0x61, 0x72, 0x21, 0x1A, 0x07], 0),
        new("application/gzip",       [0x1F, 0x8B],                     0),

        new("application/x-msdownload", [0x4D, 0x5A],                   0),     
        new("application/x-elf",      [0x7F, 0x45, 0x4C, 0x46],         0),     
        new("application/x-mach-binary",[0xCF, 0xFA, 0xED, 0xFE],       0),     
        new("application/x-mach-binary",[0xCE, 0xFA, 0xED, 0xFE],       0),     
        new("application/x-sh",       [0x23, 0x21],                     0),     
        new("video/mp4",              [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], 0),
        new("video/avi",              [0x52, 0x49, 0x46, 0x46],         0),     

        new("text/xml",               [0x3C, 0x3F, 0x78, 0x6D, 0x6C],  0),     
        new("text/html",              [0x3C, 0x21, 0x44, 0x4F, 0x43],  0),     

    }.OrderByDescending(s => s.Bytes.Length).ToList();


    private static readonly HashSet<string> MimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/tiff",
        "text/plain",
        "text/csv"
    };


    private static readonly HashSet<string> MimeBloqueados = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-msdownload",
        "application/x-executable",
        "application/x-elf",
        "application/x-mach-binary",
        "application/x-sh",
        "application/x-bat",
        "application/x-dosexec",
        "application/octet-stream", 
        "text/javascript",
        "application/javascript",
        "text/html",                
        "application/x-httpd-php"
    };


    private static readonly Dictionary<string, HashSet<string>> ExtensaoParaMime =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"]  = ["application/pdf"],
            ["doc"]  = ["application/msword"],
            ["docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        "application/zip"],
            ["xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "application/zip"],
            ["pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        "application/zip"],
            ["jpg"]  = ["image/jpeg"],
            ["jpeg"] = ["image/jpeg"],
            ["png"]  = ["image/png"],
            ["gif"]  = ["image/gif"],
            ["webp"] = ["image/webp"],
            ["tiff"] = ["image/tiff"],
            ["tif"]  = ["image/tiff"],
            ["txt"]  = ["text/plain"],
            ["csv"]  = ["text/plain", "text/csv"]
        };


    public async Task<ResultadoMimeSniff> AnalisarAsync(
        Stream stream,
        string nomeOriginal,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeOriginal);

        var posicaoOriginal = stream.CanSeek ? stream.Position : 0L;

        var magicBytes = new byte[MagicBytesLength];
        var bytesLidos = await stream.ReadAsync(magicBytes.AsMemory(0, MagicBytesLength), ct);

        if (stream.CanSeek)
            stream.Seek(posicaoOriginal, SeekOrigin.Begin);

        var extensao = Path.GetExtension(nomeOriginal).TrimStart('.').ToLowerInvariant();
        var mimeDetectado = DetectarMime(magicBytes.AsSpan(0, bytesLidos));
        var mimeDeclarado = ExtensaoParaMime.TryGetValue(extensao, out var mimesEsperados)
            ? mimesEsperados.FirstOrDefault()
            : null;

        if (MimeBloqueados.Contains(mimeDetectado))
        {
            _logger.LogCritical(
                "MIME SNIFFING: Ficheiro malicioso bloqueado! " +
                "Nome={Nome} Extensao={Extensao} MimeDetectado={Mime}",
                nomeOriginal, extensao, mimeDetectado);

            return new ResultadoMimeSniff(
                Permitido: false,
                MimeTypeDetectado: mimeDetectado,
                MimeTypeDeclarado: mimeDeclarado,
                MimeConflito: true,
                MotivoBloqueo: $"Tipo de ficheiro bloqueado por política de segurança: {mimeDetectado}");
        }

        var mimeConflito = false;
        if (mimesEsperados is not null && !mimesEsperados.Contains(mimeDetectado))
        {
            mimeConflito = true;
            _logger.LogWarning(
                "MIME SNIFFING: Conflito detectado! Extensão={Extensao} sugere {MimeEsperado} " +
                "mas magic bytes indicam {MimeReal}. Ficheiro={Nome}",
                extensao, mimeDeclarado, mimeDetectado, nomeOriginal);
        }

        if (!MimePermitidos.Contains(mimeDetectado))
        {
            _logger.LogWarning(
                "MIME SNIFFING: Tipo não permitido. Nome={Nome} Mime={Mime}",
                nomeOriginal, mimeDetectado);

            return new ResultadoMimeSniff(
                Permitido: false,
                MimeTypeDetectado: mimeDetectado,
                MimeTypeDeclarado: mimeDeclarado,
                MimeConflito: mimeConflito,
                MotivoBloqueo: $"Tipo de ficheiro não permitido: {mimeDetectado}");
        }

        _logger.LogDebug(
            "MIME SNIFFING: Ficheiro aceite. Nome={Nome} Mime={Mime} Conflito={Conflito}",
            nomeOriginal, mimeDetectado, mimeConflito);

        return new ResultadoMimeSniff(
            Permitido: true,
            MimeTypeDetectado: mimeDetectado,
            MimeTypeDeclarado: mimeDeclarado,
            MimeConflito: mimeConflito,
            MotivoBloqueo: null);
    }


    private static string DetectarMime(ReadOnlySpan<byte> bytes)
    {
        foreach (var sig in Signatures)
        {
            if (bytes.Length < sig.Offset + sig.Bytes.Length)
                continue;

            var slice = bytes.Slice(sig.Offset, sig.Bytes.Length);
            if (slice.SequenceEqual(sig.Bytes))
            {
                if (sig.MimeType == "image/webp" && bytes.Length >= 12)
                {
                    var webpMarker = bytes.Slice(8, 4);
                    if (!webpMarker.SequenceEqual("WEBP"u8))
                        continue; 
                }

                return sig.MimeType;
            }
        }

        if (bytes.Length > 0 && EhTextoPlano(bytes))
            return "text/plain";

        return "application/octet-stream"; 
    }

    private static bool EhTextoPlano(ReadOnlySpan<byte> bytes)
    {
        var amostra = bytes[..Math.Min(bytes.Length, 512)];
        var bytesNulos = 0;
        foreach (var b in amostra)
        {
            if (b == 0x00) bytesNulos++;
            if (bytesNulos > 1) return false; // Binário
        }
        return true;
    }


    private sealed record MagicSignature(string MimeType, byte[] Bytes, int Offset = 0);
}
