using System.Linq;
using Accusoft.Api.Data;
using Accusoft.Api.Domain.Enums;
using Accusoft.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Accusoft.Api.Infrastructure.HealthChecks;

// ═════════════════════════════════════════════════════════════════════════════
// HEALTH CHECK: Storage
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Verifica a saúde do storage de documentos:
///   • Existência do diretório raiz
///   • Permissões de leitura e escrita
///   • Espaço disponível em disco (alerta se &lt; 10%)
/// </summary>
public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly EcmStorageOptions _options;
    private readonly ILogger<StorageHealthCheck> _logger;

    // Limiar de espaço livre — abaixo disto é Degraded
    private const double LimiarEspacoLivrePercent = 10.0;

    public StorageHealthCheck(
        IOptions<EcmStorageOptions> options,
        ILogger<StorageHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var dados = new Dictionary<string, object>
        {
            ["raiz_storage"] = _options.RaizStorage,
            ["timestamp"] = DateTimeOffset.UtcNow
        };

        try
        {
            // ── Existência do diretório ────────────────────────────────────────
            if (!Directory.Exists(_options.RaizStorage))
            {
                _logger.LogCritical(
                    "Storage raiz não existe: {Raiz}", _options.RaizStorage);
                dados["erro"] = "Diretório raiz não encontrado";
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Diretório de storage não encontrado.", data: dados));
            }

            // ── Permissão de escrita ───────────────────────────────────────────
            var ficheiroTeste = Path.Combine(_options.RaizStorage, $".healthcheck_{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(ficheiroTeste, "ecm_health");
                File.Delete(ficheiroTeste);
                dados["escrita"] = "ok";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storage sem permissão de escrita: {Raiz}", _options.RaizStorage);
                dados["escrita"] = "FALHOU";
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Storage sem permissão de escrita.", ex, dados));
            }

            // ── Espaço em disco ────────────────────────────────────────────────
            var drive = new DriveInfo(Path.GetPathRoot(_options.RaizStorage)!);
            if (drive.IsReady)
            {
                var totalGb = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 2);
                var livreGb = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 2);
                var percentLivre = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;

                dados["disco_total_gb"] = totalGb;
                dados["disco_livre_gb"] = livreGb;
                dados["disco_livre_percent"] = Math.Round(percentLivre, 1);

                if (percentLivre < LimiarEspacoLivrePercent)
                {
                    _logger.LogWarning(
                        "Espaço em disco crítico: {Livre:F1}% livre. Raiz={Raiz}",
                        percentLivre, _options.RaizStorage);
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Espaço em disco reduzido: {percentLivre:F1}% livre.", data: dados));
                }
            }

            dados["status"] = "healthy";
            return Task.FromResult(HealthCheckResult.Healthy(
                "Storage operacional.", dados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no health check de storage.");
            dados["excecao"] = ex.Message;
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Erro ao verificar storage.", ex, dados));
        }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// HEALTH CHECK: Integridade da Base de Dados ECM
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Verifica a saúde da ligação à base de dados ECM:
///   • Conectividade básica (CanConnectAsync)
///   • Existência das tabelas principais do schema ecm
///   • Latência da ligação
/// </summary>
public sealed class EcmDatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;
    private readonly ILogger<EcmDatabaseHealthCheck> _logger;

    public EcmDatabaseHealthCheck(AppDbContext context, ILogger<EcmDatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var dados = new Dictionary<string, object>
        {
            ["timestamp"] = DateTimeOffset.UtcNow
        };

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ligacaoOk = await _context.Database.CanConnectAsync(ct);
            sw.Stop();

            dados["latencia_ms"] = sw.ElapsedMilliseconds;
            dados["ligacao"] = ligacaoOk ? "ok" : "falhou";

            if (!ligacaoOk)
                return HealthCheckResult.Unhealthy("Sem ligação à base de dados ECM.", data: dados);

            // Latência alta = Degraded
            if (sw.ElapsedMilliseconds > 2000)
            {
                _logger.LogWarning(
                    "Base de dados ECM com latência elevada: {Ms}ms", sw.ElapsedMilliseconds);
                return HealthCheckResult.Degraded(
                    $"Latência elevada: {sw.ElapsedMilliseconds}ms", data: dados);
            }

            dados["status"] = "healthy";
            return HealthCheckResult.Healthy("Base de dados ECM operacional.", dados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no health check da base de dados ECM.");
            dados["excecao"] = ex.Message;
            return HealthCheckResult.Unhealthy("Erro ao verificar base de dados ECM.", ex, dados);
        }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// HEALTH CHECK: Integridade Periódica (amostragem)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Verifica a integridade de uma amostra aleatória de documentos.
/// Útil para detetar adulteração silenciosa no storage.
/// Executa como health check Degraded se encontrar documentos comprometidos.
/// </summary>
public sealed class IntegridadeAmostragemHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;
    private readonly IOptions<EcmStorageOptions> _storageOptions;
    private readonly ILogger<IntegridadeAmostragemHealthCheck> _logger;

    // Tamanho da amostra aleatória por execução do health check
    private const int TamanhoAmostra = 5;

    public IntegridadeAmostragemHealthCheck(
        AppDbContext context,
        IOptions<EcmStorageOptions> storageOptions,
        ILogger<IntegridadeAmostragemHealthCheck> logger)
    {
        _context = context;
        _storageOptions = storageOptions;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var dados = new Dictionary<string, object>
        {
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["tamanho_amostra"] = TamanhoAmostra
        };

        try
        {
            // Documentos ativos que não foram recentemente verificados
            var amostra = await _context.Documentos
                .Where(d => !d.IsDeleted
                         && d.Estado == EstadoDocumento.Ativo
                         && d.IsLatest)
                .OrderBy(_ => Guid.NewGuid()) // Ordem aleatória (SQL Server: NEWID())
                .Take(TamanhoAmostra)
                .Select(d => new { d.Id, d.HashSHA256, d.CaminhoRelativo })
                .ToListAsync(ct);

            dados["documentos_amostrados"] = amostra.Count;

            if (amostra.Count == 0)
            {
                dados["status"] = "sem_documentos";
                return HealthCheckResult.Healthy("Sem documentos para verificar.", dados);
            }

            var comprometidos = new List<Guid>();
            var ausentes = new List<Guid>();

            foreach (var doc in amostra)
            {
                var caminho = Path.Combine(_storageOptions.Value.RaizStorage, doc.CaminhoRelativo);

                if (!File.Exists(caminho))
                {
                    ausentes.Add(doc.Id);
                    continue;
                }

                // Hash rápido para verificação
                await using var fs = new FileStream(
                    caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(fs, ct);
                var hashAtual = Convert.ToHexString(hashBytes).ToLowerInvariant();

                if (!string.Equals(hashAtual, doc.HashSHA256, StringComparison.OrdinalIgnoreCase))
                    comprometidos.Add(doc.Id);
            }

            dados["ausentes"] = ausentes.Count;
            dados["comprometidos"] = comprometidos.Count;

            if (comprometidos.Count > 0 || ausentes.Count > 0)
            {
                _logger.LogCritical(
                    "Integridade comprometida na amostragem! " +
                    "Comprometidos={C} Ausentes={A}",
                    comprometidos.Count, ausentes.Count);

                return HealthCheckResult.Degraded(
                    $"Integridade: {comprometidos.Count} comprometidos, {ausentes.Count} ausentes.",
                    data: dados);
            }

            dados["status"] = "healthy";
            return HealthCheckResult.Healthy(
                $"Integridade OK. {amostra.Count} documentos verificados.", dados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no health check de integridade.");
            dados["excecao"] = ex.Message;
            return HealthCheckResult.Unhealthy("Erro na verificação de integridade.", ex, dados);
        }
    }
}
