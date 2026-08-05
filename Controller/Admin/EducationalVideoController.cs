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
    public class EducationalVideoController : VappControllerBase
    {
        /// <summary>
        /// سقف بدنه درخواست کمی بالاتر از سقف فایل است تا overhead فرم multipart باعث 413 نشود.
        /// </summary>
        private const long MaxVideoBodyBytes = (2L * 1024 * 1024 * 1024) + (64L * 1024 * 1024); // 2 GB + 64 MB

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
        public async Task<ActionResult<ApiResponse<List<EducationalVideoResponseDto>>>> GetAll([FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<EducationalVideoResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد ویدیو آموزشی (multipart — فایل ویدیو یا لینک)
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxVideoBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxVideoBodyBytes)]
        public async Task<ActionResult<ApiResponse<EducationalVideoResponseDto>>> Create(
            [FromForm] CreateEducationalVideoDto dto,
            CancellationToken cancellationToken)
        {
            var invalid = InvalidModelStateResponse<EducationalVideoResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.CreateAsync(dto, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی ویدیو آموزشی (multipart — فایل ویدیو یا لینک)
        /// </summary>
        [HttpPost("{id:int}/update")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxVideoBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxVideoBodyBytes)]
        public async Task<ActionResult<ApiResponse<EducationalVideoResponseDto>>> Update(
            int id,
            [FromForm] UpdateEducationalVideoDto dto,
            CancellationToken cancellationToken)
        {
            var invalid = InvalidModelStateResponse<EducationalVideoResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
