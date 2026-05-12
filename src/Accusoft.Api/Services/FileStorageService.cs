namespace Accusoft.Api.Services;

public interface IFileStorageService
{
    Task<(string PathUrl, long TamanhoBytes, string Tipo)> SaveAsync(IFormFile file, int userId);
    void Delete(string pathUrl);
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
        { ".zip",  "arquivo" },
        { ".rar",  "arquivo" },
    };

  public IConfiguration Config { get; } = config;

  public async Task<(string PathUrl, long TamanhoBytes, string Tipo)> SaveAsync(IFormFile file, int userId)
    {
        var uploadsRoot = Path.Combine(env.ContentRootPath, "uploads", userId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsRoot, safeName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        var tipo = ExtToTipo.GetValueOrDefault(ext, "outro");
        var pathUrl = $"/uploads/{userId}/{safeName}";
        return (pathUrl, file.Length, tipo);
    }
    public void Delete(string pathUrl)
    {
        var full = Path.Combine(env.ContentRootPath, pathUrl.TrimStart('/'));
        if (File.Exists(full)) File.Delete(full);
    }
}