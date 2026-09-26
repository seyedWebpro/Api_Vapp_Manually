using Api_Vapp.Constants;
using Api_Vapp.Attributes;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    [ApiController]
    [Route("api/professional-campaigns")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.Messaging)]
    public class ProfessionalCampaignController : VappControllerBase
    {
        private readonly IProfessionalCampaignService _service;

        public ProfessionalCampaignController(
            IProfessionalCampaignService service,
            IConfiguration configuration,
            IUserRepository userRepository) : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> Create(
            [FromBody] CreateProfessionalCampaignDto dto)
        {
            var invalid = InvalidModelStateResponse<ProfessionalCampaignResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.CreateAsync(await GetCurrentUserIdAsync(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignListResponseDto>>> GetList(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetListAsync(await GetCurrentUserIdAsync(), pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(await GetCurrentUserIdAsync(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/activate")]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> Activate(int id)
        {
            var result = await _service.ActivateAsync(await GetCurrentUserIdAsync(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/pause")]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> Pause(int id)
        {
            var result = await _service.PauseAsync(await GetCurrentUserIdAsync(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/resume")]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> Resume(int id)
        {
            var result = await _service.ResumeAsync(await GetCurrentUserIdAsync(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancel(int id)
        {
            var result = await _service.CancelAsync(await GetCurrentUserIdAsync(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/steps/{stepId:int}/retry")]
        public async Task<ActionResult<ApiResponse<ProfessionalCampaignResponseDto>>> RetryFailedStep(int id, int stepId)
        {
            var result = await _service.RetryFailedStepAsync(await GetCurrentUserIdAsync(), id, stepId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
