using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeuNamespace.Models.DTOs;
using SeuNamespace.Services;
using Accusoft.Api.Extensions;

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

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            var uid = User.GetUserId();
            var stats = await _dashboardService.GetDashboardStatsAsync(uid);
            return Ok(stats);
        }

        [HttpGet("atividades-recentes")]
        public async Task<ActionResult<List<AtividadeRecenteDto>>> GetAtividadesRecentes([FromQuery] int limite = 5)
        {
            var uid = User.GetUserId();
            var atividades = await _dashboardService.GetAtividadesRecentesAsync(uid, limite);
            return Ok(atividades);
        }
    }
}



namespace SeuNamespace.Controllers
{
    [ApiController]
    [Route("api/user/dashboard/gestao-viagens")]
    [Authorize]
    public class GestaoViagensController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public GestaoViagensController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }


        [HttpGet]
        public async Task<ActionResult<PaginatedResponseDto<ViagemEmCursoDto>>> GetViagens(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (status == "EmCurso")
            {
                var uid = User.GetUserId();
                var viagens = await _dashboardService.GetViagensEmCursoAsync(uid, page, pageSize);
                return Ok(viagens);
            }

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
    [Route("api/user/dashboard/incidentes")]
    [Authorize]
    public class IncidentesController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public IncidentesController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }


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



namespace Accusoft.Api.Controllers
{
    [ApiController]
    [Route("api/user/dashboard/faturas")]
    [Authorize]
    public class DashboardFaturasController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardFaturasController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<ActionResult<List<FaturaRecenteDto>>> GetFaturas([FromQuery] int pageSize = 10)
        {
            var uid = User.GetUserId();
            var faturas = await _dashboardService.GetFaturasRecentesAsync(uid, pageSize);
            return Ok(faturas);
        }
    }
}