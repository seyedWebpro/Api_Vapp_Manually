using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SubscriptionPlanController : VappControllerBase
    {
        private readonly IAdminSubscriptionPlanService _service;

        public SubscriptionPlanController(
            IAdminSubscriptionPlanService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<SubscriptionPlanResponseDto>>>> GetActivePlans()
        {
            var result = await _service.GetActivePlansAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
