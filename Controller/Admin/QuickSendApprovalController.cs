using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// تأیید یک‌باره محتوای ارسال سریع (کارت ویزیت، رزرو، فرم، گردونه، لینک، اقدام سریع)
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class QuickSendApprovalController : VappControllerBase
    {
        private readonly IAdminQuickSendApprovalService _service;

        public QuickSendApprovalController(
            IAdminQuickSendApprovalService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>لیست آیتم‌های در انتظار تأیید</summary>
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>>> GetPending(
            [FromQuery] string? itemType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetPendingAsync(itemType, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لیست همه / فیلتر وضعیت و نوع</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] string? itemType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetAllAsync(status, itemType, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>جزئیات یک آیتم</summary>
        [HttpGet("{itemType}/{id:int}")]
        public async Task<ActionResult<ApiResponse<QuickSendApprovalResponseDto>>> GetById(
            string itemType,
            int id)
        {
            var result = await _service.GetByIdAsync(itemType, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>تأیید — بعد از این، ارسال سریع بدون صف پیام</summary>
        [HttpPost("{itemType}/{id:int}/approve")]
        public async Task<ActionResult<ApiResponse<bool>>> Approve(string itemType, int id)
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ApproveAsync(itemType, id, adminUserId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>رد با دلیل</summary>
        [HttpPost("{itemType}/{id:int}/reject")]
        public async Task<ActionResult<ApiResponse<bool>>> Reject(
            string itemType,
            int id,
            [FromBody] RejectApprovalDto dto)
        {
            var invalid = InvalidModelStateResponse<bool>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.RejectAsync(itemType, id, adminUserId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
