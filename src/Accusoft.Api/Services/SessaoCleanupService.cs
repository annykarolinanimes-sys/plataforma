// Services/SessaoCleanupService.cs - Versão simplificada
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Accusoft.Api.Services;

public class SessaoCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessaoCleanupService> _logger;

    public SessaoCleanupService(IServiceProvider serviceProvider, ILogger<SessaoCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguardar a aplicação iniciar completamente
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<Accusoft.Api.Data.AppDbContext>();
                    
                    // Limpar sessões diretamente sem usar o serviço
                    var expiradas = dbContext.Sessoes
                        .Where(s => s.IsActive && s.DataExpiracao < DateTimeOffset.UtcNow)
                        .ToList();
                    
                    foreach (var sessao in expiradas)
                    {
                        sessao.IsActive = false;
                    }
                    
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Limpeza de {Count} sessões expiradas", expiradas.Count);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar sessões expiradas");
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}