using Microsoft.AspNetCore.Http;

namespace Accusoft.Api.Services;

public interface IFileStorageService
{
    Task<(string PathUrl, long TamanhoBytes, string Tipo)> SaveAsync(IFormFile file, int userId);
    void Delete(string pathUrl);
    Stream? GetStream(string pathUrl);
}

public class LocalFileStorageService(IWebHostEnvironment env, IConfiguration config) : IFileStorageService
{
    private static readonly Dictionary<string, string> ExtToTipo = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf",  "pdf" },
        { ".docx", "docx" },
        { ".doc",  "docx" },
        { ".xlsx", "xlsx" },
        { ".xls",  "xlsx" },
        { ".jpg",  "imagem" },
        { ".jpeg", "imagem" },
        { ".png",  "imagem" },
        { ".gif",  "imagem" },
        { ".webp", "imagem" },
        { ".zip",  "arquivo" },
        { ".rar",  "arquivo" },
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls",
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".zip", ".rar"
    };

    public IConfiguration Config { get; } = config;

    public async Task<(string PathUrl, long TamanhoBytes, string Tipo)> SaveAsync(IFormFile file, int userId)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Ficheiro inválido ou vazio.", nameof(file));

        if (userId <= 0)
            throw new ArgumentException("UserId inválido.", nameof(userId));

        // Sanitize and validate extension
        var originalExt = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (string.IsNullOrEmpty(originalExt) || !AllowedExtensions.Contains(originalExt))
            throw new InvalidOperationException($"Extensão de ficheiro não permitida: '{originalExt}'.");

        // Build a safe upload path using only the userId (no user-supplied path components)
        var uploadsRoot = Path.GetFullPath(
            Path.Combine(env.ContentRootPath, "uploads", userId.ToString()));

        // Ensure the resolved path is actually inside our uploads directory
        var uploadsBase = Path.GetFullPath(Path.Combine(env.ContentRootPath, "uploads"));
        if (!uploadsRoot.StartsWith(uploadsBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de upload inválido (path traversal detectado).");

        Directory.CreateDirectory(uploadsRoot);

        // Generate a random filename — never use original name to avoid path traversal
        var safeName = $"{Guid.NewGuid():N}{originalExt}";
        var fullPath = Path.Combine(uploadsRoot, safeName);

        // Final check: ensure the full file path is within the uploads root
        var resolvedFull = Path.GetFullPath(fullPath);
        if (!resolvedFull.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de destino inválido.");

        await using var stream = new FileStream(resolvedFull, FileMode.CreateNew, FileAccess.Write);
        await file.CopyToAsync(stream);

        var tipo = ExtToTipo.GetValueOrDefault(originalExt, "outro");
        var pathUrl = $"/uploads/{userId}/{safeName}";
        return (pathUrl, file.Length, tipo);
    }

    public void Delete(string pathUrl)
    {
        if (string.IsNullOrWhiteSpace(pathUrl))
            return;

        // Sanitize the path — reject anything with traversal sequences
        if (pathUrl.Contains("..") || pathUrl.Contains("//"))
            return;

        var uploadsBase = Path.GetFullPath(Path.Combine(env.ContentRootPath, "uploads"));
        var relativePart = pathUrl.TrimStart('/');

        // Prevent path traversal in the URL
        if (relativePart.Contains(".."))
            return;

        var fullPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, relativePart));

        // Ensure the file is inside the uploads directory
        if (!fullPath.StartsWith(uploadsBase, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); }
            catch { /* log in production if needed but non-critical */ }
        }
    }

    public Stream? GetStream(string pathUrl)
    {
        if (string.IsNullOrWhiteSpace(pathUrl))
            return null;

        if (pathUrl.Contains("..") || pathUrl.Contains("//"))
            return null;

        var uploadsBase = Path.GetFullPath(Path.Combine(env.ContentRootPath, "uploads"));
        var relativePart = pathUrl.TrimStart('/');
        if (relativePart.Contains(".."))
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, relativePart));
        if (!fullPath.StartsWith(uploadsBase, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!File.Exists(fullPath))
            return null;

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
