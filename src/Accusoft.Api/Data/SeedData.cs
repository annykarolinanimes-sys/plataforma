using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accusoft.Api;

public static class SeedData
{
    public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existeAdmin = await context.Users.AnyAsync(u => u.Role == UserRole.Admin);

        if (!existeAdmin)
        {
            logger.LogInformation("Nenhum administrador encontrado. Criando admin padrão...");

            var adminEmail = "admin@accusoft.com";
            // NOTE: In production override this via environment variable or secrets manager
            var adminSenha = Environment.GetEnvironmentVariable("ACCUSOFT_ADMIN_PASSWORD")
                             ?? "Admin123!";

            var usuarioExistente = await context.Users
                .FirstOrDefaultAsync(u => u.Email == adminEmail);

            if (usuarioExistente == null)
            {
                var senhaHash = BCrypt.Net.BCrypt.HashPassword(adminSenha, workFactor: 11);

                var adminUser = new User
                {
                    Nome = "Administrador do Sistema",
                    Email = adminEmail,
                    SenhaHash = senhaHash,
                    Role = UserRole.Admin,
                    Status = UserStatus.Ativo,
                    Departamento = "Tecnologia da Informação",
                    Cargo = "Administrador do Sistema",
                    Telefone = "(00) 0000-0000",
                    DataCriacao = DateTimeOffset.UtcNow,
                    UltimoLogin = null
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                // Do NOT log the actual password — log a reminder instead
                logger.LogInformation("Admin criado com sucesso! Email: {AdminEmail}", adminEmail);
                logger.LogWarning(
                    "SEGURANÇA: A senha do administrador foi definida. " +
                    "Por favor, altere-a após o primeiro login. " +
                    "Em produção, defina a variável de ambiente ACCUSOFT_ADMIN_PASSWORD.");
            }
            else
            {
                if (usuarioExistente.Role != UserRole.Admin)
                {
                    usuarioExistente.Role = UserRole.Admin;
                    await context.SaveChangesAsync();
                    logger.LogInformation(
                        "Utilizador {AdminEmail} foi promovido a administrador.", adminEmail);
                }
                else
                {
                    logger.LogInformation(
                        "Já existe um utilizador administrador com o email {AdminEmail}.", adminEmail);
                }
            }
        }
        else
        {
            logger.LogDebug("Já existe um administrador no sistema. Seed não necessário.");
        }
    }
}
