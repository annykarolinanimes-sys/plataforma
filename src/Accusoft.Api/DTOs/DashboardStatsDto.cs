namespace SeuNamespace.Models.DTOs
{
    public class DashboardStatsDto
    {
        public decimal ValorTotalFaturasMes { get; set; }
        public int ViagensAtivas { get; set; }
        public int TotalClientes { get; set; }
        public int IncidentesPendentes { get; set; }
    }
}

namespace SeuNamespace.Models.DTOs
{
    public class AtividadeRecenteDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; 
        public string Status { get; set; } = string.Empty; 
        public DateTimeOffset Data { get; set; }
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }
}

namespace SeuNamespace.Models.DTOs
{
    public class ViagemEmCursoDto
    {
        public int Id { get; set; }
        public string NumeroViagem { get; set; } = string.Empty;
        public string Origem { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public int Progresso { get; set; }
        public int UsuarioId { get; set; }
    }
}

namespace SeuNamespace.Models.DTOs
{
    public class IncidentePendenteDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Gravidade { get; set; } = string.Empty; 
        public DateTime DataOcorrencia { get; set; }
    }
}

namespace SeuNamespace.Models.DTOs
{
    public class FaturaRecenteDto
    {
        public int Id { get; set; }
        public string NumeroFatura { get; set; } = string.Empty;
        public string ClienteNome { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public DateTime DataDoc { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
    }
}

namespace SeuNamespace.Models.DTOs
{
    public class PaginatedResponseDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}