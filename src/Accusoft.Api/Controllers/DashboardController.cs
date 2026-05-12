using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeuNamespace.Models.DTOs;
using SeuNamespace.Services;

namespace SeuNamespace.Controllers
{
    [ApiController]
    [Route("api/user/dashboard")]
    [Authorize] 
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// GET /api/user/dashboard/stats
        /// Retorna estatísticas gerais para o dashboard
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            var stats = await _dashboardService.GetDashboardStatsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// GET /api/user/dashboard/atividades-recentes?limite=5
        /// Retorna lista de atividades recentes
        /// </summary>
        [HttpGet("atividades-recentes")]
        public async Task<ActionResult<List<AtividadeRecenteDto>>> GetAtividadesRecentes([FromQuery] int limite = 5)
        {
            var atividades = await _dashboardService.GetAtividadesRecentesAsync(limite);
            return Ok(atividades);
        }
    }
}



namespace SeuNamespace.Controllers
{
    [ApiController]
    [Route("api/user/gestao-viagens")]
    [Authorize]
    public class GestaoViagensController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public GestaoViagensController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// GET /api/user/gestao-viagens?status=EmCurso&page=1&pageSize=5
        /// Retorna viagens com paginação e filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponseDto<ViagemEmCursoDto>>> GetViagens(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (status == "EmCurso")
            {
                var viagens = await _dashboardService.GetViagensEmCursoAsync(page, pageSize);
                return Ok(viagens);
            }

            // Implementar outros filtros conforme necessário
            return Ok(new PaginatedResponseDto<ViagemEmCursoDto>
            {
                Items = new List<ViagemEmCursoDto>(),
                Total = 0,
                Page = page,
                PageSize = pageSize
            });
        }
    }
}


namespace SeuNamespace.Controllers
{
    [ApiController]
    [Route("api/user/incidentes")]
    [Authorize]
    public class IncidentesController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public IncidentesController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// GET /api/user/incidentes?status=Aberto&page=1&pageSize=5
        /// Retorna incidentes com paginação e filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponseDto<IncidentePendenteDto>>> GetIncidentes(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (status == "Aberto")
            {
                var incidentes = await _dashboardService.GetIncidentesPendentesAsync(page, pageSize);
                return Ok(incidentes);
            }

            return Ok(new PaginatedResponseDto<IncidentePendenteDto>
            {
                Items = new List<IncidentePendenteDto>(),
                Total = 0,
                Page = page,
                PageSize = pageSize
            });
        }
    }
}



namespace SeuNamespace.Controllers
{
    [ApiController]
    [Route("api/user/faturas")]
    [Authorize]
    public class FaturasController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public FaturasController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// GET /api/user/faturas?pageSize=5
        /// Retorna faturas recentes
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<FaturaRecenteDto>>> GetFaturas([FromQuery] int pageSize = 10)
        {
            var faturas = await _dashboardService.GetFaturasRecentesAsync(pageSize);
            return Ok(faturas);
        }
    }
}