using Api_Vapp.DTOs.Audit;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// جستجوی ردپای audit از DB (فقط بک‌اند — بدون UI).
    /// </summary>
    [ApiController]
    [Route("api/Admin/Audit")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AdminAuditController : VappControllerBase
    {
        private readonly IAuditQueryService _auditQueryService;

        public AdminAuditController(
            IAuditQueryService auditQueryService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _auditQueryService = auditQueryService;
        }

        /// <summary>
        /// جستجوی صفحه‌بندی‌شده audit با فیلترهای استاندارد
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<AuditLogDto>>>> Search(
            [FromQuery] AuditSearchRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _auditQueryService.SearchAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات یک رکورد audit
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ApiResponse<AuditLogDto>>> GetById(
            long id,
            CancellationToken cancellationToken)
        {
            var result = await _auditQueryService.GetByIdAsync(id, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
