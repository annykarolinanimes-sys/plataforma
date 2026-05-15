using Accusoft.Api.Middleware;
using Accusoft.Api.Services;
using Accusoft.Api.Infrastructure.HealthChecks;
using Accusoft.Api.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Accusoft.Api.Infrastructure;

// ═════════════════════════════════════════════════════════════════════════════
// REGISTO DE DEPENDÊNCIAS — MÓDULO ECM COMPLETO
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Registo centralizado de todas as dependências do módulo ECM.
///
/// Invocar em Program.cs:
///   builder.Services.AddEcmModulo(builder.Configuration);
///   ...
///   app.UseEcmModulo();
/// </summary>
public static class EcmModuloRegistration
{
    // ─── Services Registration ────────────────────────────────────────────────

    public static IServiceCollection AddEcmModulo(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext / Unit of Work ───────────────────────────────────────────
            // Se AppDbContext já foi registado externamente, mantém a configuração existente.
            if (!services.Any(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)))
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(
                        configuration.GetConnectionString("DefaultConnection"),
                        sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure(
                                5,
                                TimeSpan.FromSeconds(10),
                                null);
                            sqlOptions.CommandTimeout(60);
                            sqlOptions.MigrationsHistoryTable(
                                "__EFMigrationsHistory", schema: "public");
                            // Habilitar split queries para coleções aninhadas (Historico, Auditorias)
                            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        });

    #if DEBUG
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
    #endif
                });
            }
        // ── Serviços de Aplicação ─────────────────────────────────────────────
        services.AddScoped<IDocumentoService, DocumentoService>();
        services.AddScoped<IIntegridadeService, IntegridadeService>();
        services.AddScoped<IMimeSniffingService, MimeSniffingService>();

        // ── Health Checks ─────────────────────────────────────────────────────
        services.AddEcmHealthChecks();

        return services;
    }

    // ─── Health Checks ────────────────────────────────────────────────────────

    private static IServiceCollection AddEcmHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            // Storage: existência, permissões e espaço em disco
            .AddCheck<StorageHealthCheck>(
                name: "ecm-storage",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ecm", "storage", "ready"])

            // Base de dados: conectividade e latência
            .AddCheck<EcmDatabaseHealthCheck>(
                name: "ecm-database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ecm", "database", "ready"])

            // Integridade: amostragem periódica de hash SHA-256
            .AddCheck<IntegridadeAmostragemHealthCheck>(
                name: "ecm-integridade",
                failureStatus: HealthStatus.Degraded,  // Degraded, não Unhealthy
                tags: ["ecm", "integridade"]);

        return services;
    }

    // ─── Middleware Pipeline ──────────────────────────────────────────────────

    /// <summary>
    /// Configura o pipeline de middleware do módulo ECM.
    ///
    /// Ordem obrigatória (a inversão quebra rastreabilidade):
    ///   1. ExceptionMiddleware   — captura todas as exceções
    ///   2. AuditoriaMiddleware   — injeta CorrelationId + regista auditoria
    ///   3. Authentication/Authorization (já no pipeline ASP.NET Core)
    ///   4. Controllers
    /// </summary>
    public static WebApplication UseEcmModulo(this WebApplication app)
    {
        // 1. Tratamento global de exceções ECM (deve ser o primeiro)
        app.UseMiddleware<EcmExceptionMiddleware>();

        // 2. Auditoria automática por request
        app.UseMiddleware<AuditoriaMiddleware>();

        // 3. Health Check endpoints
        app.MapHealthChecks("/health/ecm", new HealthCheckOptions
        {
            // Apenas health checks com tag "ecm"
            Predicate = check => check.Tags.Contains("ecm"),
            ResponseWriter = EcmHealthCheckResponseWriter.EscreverAsync
        });

        // Endpoint de readiness (para Kubernetes/Docker)
        app.MapHealthChecks("/health/ecm/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = EcmHealthCheckResponseWriter.EscreverAsync
        });

        return app;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// HEALTH CHECK RESPONSE WRITER — JSON estruturado
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Serializa o resultado dos health checks em JSON estruturado e legível.
/// Inclui detalhes de cada check individual para dashboards de monitorização.
/// </summary>
public static class EcmHealthCheckResponseWriter
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static async Task EscreverAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var resposta = new
        {
            status = report.Status.ToString(),
            duracao = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description,
                duracao_ms = e.Value.Duration.TotalMilliseconds,
                dados = e.Value.Data,
                excecao = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(resposta, JsonOptions));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PROGRAM.CS — EXEMPLO DE WIRING COMPLETO
// ═════════════════════════════════════════════════════════════════════════════

/*
// ─── Program.cs (exemplo de integração) ──────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Serilog — logging estruturado JSON (OpenTelemetry-ready)
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(ctx.Configuration["Serilog:Seq:Url"]!)
    .WriteTo.File("logs/ecm-.log",
        rollingInterval: RollingInterval.Day,
        formatter: new JsonFormatter()));

// Autenticação JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience  = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization(options => {
    // Políticas RBAC granulares por permissão documental
    options.AddPolicy("Documento.View",     p => p.RequireClaim("permission", "Documento.View"));
    options.AddPolicy("Documento.Download", p => p.RequireClaim("permission", "Documento.Download"));
    options.AddPolicy("Documento.Upload",   p => p.RequireClaim("permission", "Documento.Upload"));
    options.AddPolicy("Documento.Validate", p => p.RequireClaim("permission", "Documento.Validate"));
    options.AddPolicy("Documento.Delete",   p => p.RequireClaim("permission", "Documento.Delete"));
    options.AddPolicy("Documento.Archive",  p => p.RequireClaim("permission", "Documento.Archive"));
    options.AddPolicy("Documento.History",  p => p.RequireClaim("permission", "Documento.History"));
});

// Módulo ECM completo
builder.Services.AddEcmModulo(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Pipeline ECM (ExceptionMiddleware + AuditoriaMiddleware + HealthChecks)
app.UseEcmModulo();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

// ─── appsettings.json (secções ECM) ─────────────────────────────────────────

// {
//   "ConnectionStrings": {
//     "EcmDatabase": "Server=.;Database=EcmDb;Trusted_Connection=True;"
//   },
//   "Ecm": {
//     "Storage": {
//       "RaizStorage": "/var/ecm/storage",
//       "TamanhoMaximoBytes": 52428800
//     }
//   },
//   "Auth": {
//     "Authority": "https://auth.empresa.pt",
//     "Audience":  "ecm-api"
//   }
// }
*/
