using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// هاب دارایی‌های کاربر برای ادمین — قالب‌ها و محتوای ارسال سریع (فاز ۱)
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class UserInventoryController : VappControllerBase
    {
        private readonly IAdminUserInventoryService _service;

        public UserInventoryController(
            IAdminUserInventoryService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// خلاصه شمارنده‌ها و هویت کاربر
        /// </summary>
        [HttpGet("{userId:int}/summary")]
        public async Task<ActionResult<ApiResponse<AdminUserInventorySummaryDto>>> GetSummary(int userId)
        {
            var result = await _service.GetSummaryAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست قالب‌های پیام کاربر (بدون متن قالب — فقط لینک مشاهده)
        /// </summary>
        [HttpGet("{userId:int}/templates")]
        public async Task<ActionResult<ApiResponse<PagedResponse<AdminUserTemplateItemDto>>>> GetTemplates(
            int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetTemplatesAsync(userId, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست محتوای ارسال سریع کاربر (گردونه، کارت، فرم، …) شامل پیش‌نویس و غیرفعال
        /// </summary>
        [HttpGet("{userId:int}/contents")]
        public async Task<ActionResult<ApiResponse<PagedResponse<AdminUserContentItemDto>>>> GetContents(
            int userId,
            [FromQuery] string? itemType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetContentsAsync(userId, itemType, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ساخت لینک مشاهده محتوا برای ادمین (عمومی / پیش‌نمایش ادمین / خارجی)
        /// </summary>
        [HttpPost("{userId:int}/contents/{itemType}/{id:int}/view-link")]
        public async Task<ActionResult<ApiResponse<AdminUserContentViewLinkDto>>> CreateViewLink(
            int userId,
            string itemType,
            int id)
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.CreateContentViewLinkAsync(userId, itemType, id, adminUserId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
