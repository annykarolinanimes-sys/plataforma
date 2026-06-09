using SeuNamespace.Models.DTOs;
using Accusoft.Api.Data;
using Microsoft.EntityFrameworkCore;
using Accusoft.Api.Models;

namespace SeuNamespace.Services
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync(int userId);
        Task<List<AtividadeRecenteDto>> GetAtividadesRecentesAsync(int userId, int limite = 5);
        Task<PaginatedResponseDto<ViagemEmCursoDto>> GetViagensEmCursoAsync(int userId, int page, int pageSize);
        Task<PaginatedResponseDto<IncidentePendenteDto>> GetIncidentesPendentesAsync(int page, int pageSize);
        Task<List<FaturaRecenteDto>> GetFaturasRecentesAsync(int userId, int pageSize = 5);
    }

    public class DashboardService : IDashboardService
    {

        private readonly AppDbContext _db;

        public DashboardService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync(int userId)
        {
            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            var valorTotal = await _db.Faturas
                .Where(f => f.UsuarioId == userId && f.DataDoc.Year == now.Year && f.DataDoc.Month == now.Month)
                .SumAsync(f => (decimal?)f.ValorTotal) ?? 0m;

            var viagensAtivas = await _db.GestaoViagens.CountAsync(v => v.UsuarioId == userId && v.Status == "EmCurso");
            var totalClientes = await _db.ClientesCatalogo.CountAsync(c => c.CriadoPor == userId);
            var incidentesPendentes = await _db.Incidentes.CountAsync(i => i.UsuarioId == userId && i.Status == "Aberto");

            return new DashboardStatsDto
            {
                ValorTotalFaturasMes = valorTotal,
                ViagensAtivas = viagensAtivas,
                TotalClientes = totalClientes,
                IncidentesPendentes = incidentesPendentes
            };
        }

        public async Task<List<AtividadeRecenteDto>> GetAtividadesRecentesAsync(int userId, int limite = 5)
        {
            var logs = await _db.AuditLogs
                .Include(al => al.Admin)
                .Where(al => al.AdminId == userId)
                .OrderByDescending(al => al.Timestamp)
                .Take(limite * 5)
                .ToListAsync();

            return logs
                .Where(al => IsRelevantActivity(al.Acao))
                .Take(limite)
                .Select(al => new AtividadeRecenteDto
                {
                    Id = al.Id,
                    Titulo = GetFriendlyActivityTitle(al.Acao),
                    Tipo = GetActivityType(GetFriendlyActivityTitle(al.Acao)),
                    Status = string.Empty,
                    Data = al.Timestamp,
                    UsuarioId = al.AdminId,
                    Usuario = al.Admin.Nome
                })
                .ToList();
        }

        private static bool IsRelevantActivity(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return false;

            var excluded = new[]
            {
                "USER_LOGIN",
                "USER_LOGOUT",
                "SESSION_TERMINATED",
                "ALL_SESSIONS_TERMINATED"
            };

            if (excluded.Contains(action, StringComparer.OrdinalIgnoreCase))
                return false;

            var normalized = action.ToLowerInvariant();
            if (normalized.Contains("login") || normalized.Contains("logout") || normalized.Contains("sessão"))
                return false;

            return true;
        }

        private static string GetFriendlyActivityTitle(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            return action switch
            {
                "USER_LOGIN" => "Login de utilizador",
                "USER_LOGOUT" => "Logout de utilizador",
                "USER_TOGGLE" => "Utilizador alterado",
                "USER_DELETED" => "Utilizador eliminado",
                "SESSION_TERMINATED" => "Sessão terminada",
                "ALL_SESSIONS_TERMINATED" => "Todas as sessões terminadas",
                _ when action.Contains('_') => string.Join(' ', action.Split('_')).ToLowerInvariant(),
                _ when action.Contains(' ') => GetFriendlyPathAction(action),
                _ => action
            };
        }

        private static string GetFriendlyPathAction(string action)
        {
            var parts = action.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return action;

            var method = parts[0].ToUpperInvariant();
            var path = parts[1];
            var segments = path
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !int.TryParse(s, out _))
                .Where(s => s != "api" && s != "user" && s != "admin")
                .Select(s => s.Replace('-', ' '))
                .ToArray();

            if (segments.Length == 0)
                return action;

            var resource = string.Join(' ', segments);
            return method switch
            {
                "POST" => $"Criado {resource}",
                "PUT" => $"Atualizado {resource}",
                "PATCH" => $"Atualizado {resource}",
                "DELETE" => $"Eliminado {resource}",
                _ => action
            };
        }

        private static string GetActivityType(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            var normalized = GetFriendlyActivityTitle(action).ToLowerInvariant();

            if (normalized.StartsWith("post ") || normalized.Contains("criada") || normalized.Contains("criado") || normalized.Contains("created"))
                return "Criado";
            if (normalized.StartsWith("put ") || normalized.StartsWith("patch ") || normalized.Contains("atualizada") || normalized.Contains("atualizado") || normalized.Contains("alterado") || normalized.Contains("updated"))
                return "Modificado";
            if (normalized.StartsWith("delete ") || normalized.Contains("eliminada") || normalized.Contains("eliminado") || normalized.Contains("apagado") || normalized.Contains("apagou") || normalized.Contains("removido") || normalized.Contains("removida") || normalized.Contains("deleted"))
                return "Apagado";
            if (normalized.Contains("cancelada") || normalized.Contains("cancelado") || normalized.Contains("inativado") || normalized.Contains("inativada"))
                return "Inativado";
            if (normalized.Contains("iniciada") || normalized.Contains("iniciado"))
                return "Iniciado";
            if (normalized.Contains("concluída") || normalized.Contains("concluído") || normalized.Contains("resolvido") || normalized.Contains("resolvida"))
                return "Concluído";

            return "Atividade";
        }

        public async Task<PaginatedResponseDto<ViagemEmCursoDto>> GetViagensEmCursoAsync(int userId, int page, int pageSize)
        {
            var query = _db.GestaoViagens.Where(v => v.UsuarioId == userId && v.Status == "EmCurso");
            var total = await query.CountAsync();

            var items = await query.OrderBy(v => v.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new ViagemEmCursoDto
                {
                    Id = v.Id,
                    NumeroViagem = v.NumeroViagem,
                    Origem = v.Origem ?? string.Empty,
                    Destino = v.Destino ?? string.Empty,
                    Progresso = v.DistanciaTotalKm > 0 ? (int)Math.Round((double)(v.DistanciaPercorridaKm / v.DistanciaTotalKm * 100)) : 0,
                    UsuarioId = v.UsuarioId
                })
                .ToListAsync();

            return new PaginatedResponseDto<ViagemEmCursoDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedResponseDto<IncidentePendenteDto>> GetIncidentesPendentesAsync(int page, int pageSize)
        {
            var query = _db.Incidentes.Where(i => i.Status == "Aberto");
            var total = await query.CountAsync();

            var items = await query.OrderByDescending(i => i.DataOcorrencia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new IncidentePendenteDto
                {
                    Id = i.Id,
                    Titulo = i.Titulo,
                    Tipo = i.Tipo,
                    Gravidade = i.Gravidade,
                    DataOcorrencia = i.DataOcorrencia
                })
                .ToListAsync();

            return new PaginatedResponseDto<IncidentePendenteDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<FaturaRecenteDto>> GetFaturasRecentesAsync(int userId, int pageSize = 5)
        {
            var items = await _db.Faturas
                .Where(f => f.UsuarioId == userId)
                .OrderByDescending(f => f.DataDoc.Year).ThenByDescending(f => f.DataDoc.Month)
                .ThenByDescending(f => f.CriadoEm)
                .Take(pageSize)
                .Select(f => new FaturaRecenteDto
                {
                    Id = f.Id,
                    NumeroFatura = f.NumeroFatura,
                    ClienteNome = f.ClienteNome,
                    ValorTotal = f.ValorTotal,
                    DataDoc = f.DataDoc.ToDateTime(new TimeOnly(0,0)),
                    Estado = f.Estado,
                    UsuarioId = f.UsuarioId
                })
                .ToListAsync();

            return items;
        }
    }
}