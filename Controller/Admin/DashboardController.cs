using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class DashboardController : VappControllerBase
    {
        private readonly IAdminDashboardService _service;

        public DashboardController(
            IAdminDashboardService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<ApiResponse<AdminDashboardStatsDto>>> GetStats()
        {
            var result = await _service.GetStatsAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
