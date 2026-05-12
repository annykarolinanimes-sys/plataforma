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
            var adminSenha = "Admin123!";
            
            var usuarioExistente = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
            
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
                
                logger.LogInformation("Admin criado com sucesso!");
                logger.LogInformation("Email: {AdminEmail}", adminEmail);
                logger.LogInformation("Senha: {AdminPassword}", adminSenha);
                logger.LogInformation("Por favor, mude a senha após o primeiro login!");
            }
            else
            {
                if (usuarioExistente.Role != UserRole.Admin)
                {
                    usuarioExistente.Role = UserRole.Admin;
                    await context.SaveChangesAsync();
                    logger.LogInformation("Utilizador {AdminEmail} foi promovido a administrador!", adminEmail);
                }
                else
                {
                    logger.LogInformation("Já existe um utilizador com o email {AdminEmail} e ele já é administrador.", adminEmail);
                }
            }
        }
        else
        {
            logger.LogInformation("Já existe um administrador no sistema. Seed não necessário.");
        }
    }
}