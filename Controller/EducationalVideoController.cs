using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class EducationalVideoController : VappControllerBase
    {
        private readonly IAdminEducationalVideoService _service;

        public EducationalVideoController(
            IAdminEducationalVideoService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<EducationalVideoResponseDto>>>> GetActiveVideos()
        {
            var result = await _service.GetActiveVideosAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
