// Services/DashboardService.cs
using SeuNamespace.Models.DTOs;

namespace SeuNamespace.Services
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<List<AtividadeRecenteDto>> GetAtividadesRecentesAsync(int limite = 5);
        Task<PaginatedResponseDto<ViagemEmCursoDto>> GetViagensEmCursoAsync(int page, int pageSize);
        Task<PaginatedResponseDto<IncidentePendenteDto>> GetIncidentesPendentesAsync(int page, int pageSize);
        Task<List<FaturaRecenteDto>> GetFaturasRecentesAsync(int pageSize = 5);
    }

    public class DashboardService : IDashboardService
    {
        // Em produção, injetar repositórios/DBContext aqui
        // private readonly AppDbContext _context;

        public DashboardService(/* AppDbContext context */)
        {
            // _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            // TODO: Substituir por queries reais ao banco de dados
            return await Task.FromResult(new DashboardStatsDto
            {
                ValorTotalFaturasMes = 25000.50m,
                ViagensAtivas = 12,
                TotalClientes = 45,
                IncidentesPendentes = 3
            });
        }

        public async Task<List<AtividadeRecenteDto>> GetAtividadesRecentesAsync(int limite = 5)
        {
            // Mock data - substituir por query real
            var atividades = new List<AtividadeRecenteDto>
            {
                new()
                {
                    Id = 1,
                    Titulo = "Criação de fatura #FT/2025/0001",
                    Tipo = "fatura",
                    Status = "concluido",
                    Data = DateTime.UtcNow.AddMinutes(-25),
                    Usuario = "Admin"
                },
                new()
                {
                    Id = 2,
                    Titulo = "Atribuição de viagem #ATRIB/2025/02/0001",
                    Tipo = "atribuicao",
                    Status = "em_andamento",
                    Data = DateTime.UtcNow.AddHours(-1),
                    Usuario = "João Silva"
                },
                new()
                {
                    Id = 3,
                    Titulo = "Início de viagem #V/2025/02/0001",
                    Tipo = "viagem",
                    Status = "concluido",
                    Data = DateTime.UtcNow.AddHours(-2),
                    Usuario = "Carlos Santos"
                },
                new()
                {
                    Id = 4,
                    Titulo = "Reporte de incidente #INC/2025/0001",
                    Tipo = "incidente",
                    Status = "pendente",
                    Data = DateTime.UtcNow.AddHours(-3),
                    Usuario = "Maria Oliveira"
                },
                new()
                {
                    Id = 5,
                    Titulo = "Cadastro de novo produto #PROD/2025/0001",
                    Tipo = "produto",
                    Status = "concluido",
                    Data = DateTime.UtcNow.AddHours(-4),
                    Usuario = "Admin"
                }
            };

            return await Task.FromResult(atividades.Take(limite).ToList());
        }

        public async Task<PaginatedResponseDto<ViagemEmCursoDto>> GetViagensEmCursoAsync(int page, int pageSize)
        {
            var viagens = new List<ViagemEmCursoDto>
            {
                new() { Id = 1, NumeroViagem = "V/2025/02/0001", Origem = "Lisboa", Destino = "Porto", Progresso = 75 },
                new() { Id = 2, NumeroViagem = "V/2025/02/0002", Origem = "Faro", Destino = "Coimbra", Progresso = 30 },
                new() { Id = 3, NumeroViagem = "V/2025/02/0003", Origem = "Braga", Destino = "Leiria", Progresso = 90 }
            };

            return await Task.FromResult(new PaginatedResponseDto<ViagemEmCursoDto>
            {
                Items = viagens.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Total = viagens.Count,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<PaginatedResponseDto<IncidentePendenteDto>> GetIncidentesPendentesAsync(int page, int pageSize)
        {
            var incidentes = new List<IncidentePendenteDto>
            {
                new()
                {
                    Id = 1,
                    Titulo = "Avaria mecânica na viatura MAT-001",
                    Tipo = "Avaria",
                    Gravidade = "alta",
                    DataOcorrencia = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Id = 2,
                    Titulo = "Atraso na entrega - Cliente XPTO",
                    Tipo = "Atraso",
                    Gravidade = "media",
                    DataOcorrencia = DateTime.UtcNow.AddDays(-2)
                }
            };

            return await Task.FromResult(new PaginatedResponseDto<IncidentePendenteDto>
            {
                Items = incidentes.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Total = incidentes.Count,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<List<FaturaRecenteDto>> GetFaturasRecentesAsync(int pageSize = 5)
        {
            var faturas = new List<FaturaRecenteDto>
            {
                new()
                {
                    Id = 1,
                    NumeroFatura = "FT/2025/0001",
                    ClienteNome = "Empresa ABC",
                    ValorTotal = 1500.00m,
                    DataDoc = DateTime.UtcNow,
                    Estado = "Paga"
                },
                new()
                {
                    Id = 2,
                    NumeroFatura = "FT/2025/0002",
                    ClienteNome = "Transportes XYZ",
                    ValorTotal = 2300.50m,
                    DataDoc = DateTime.UtcNow.AddDays(-2),
                    Estado = "Paga"
                },
                new()
                {
                    Id = 3,
                    NumeroFatura = "FT/2025/0003",
                    ClienteNome = "Logística Rápida",
                    ValorTotal = 875.30m,
                    DataDoc = DateTime.UtcNow.AddDays(-5),
                    Estado = "Pendente"
                }
            };

            return await Task.FromResult(faturas.Take(pageSize).ToList());
        }
    }
}