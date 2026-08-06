using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Notification;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// اعلان‌های درون‌برنامه‌ای (زنگوله صفحه اصلی اپ)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class NotificationsController : VappControllerBase
    {
        private readonly IInAppNotificationService _notificationService;

        public NotificationsController(
            IInAppNotificationService notificationService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// لیست اعلان‌های کاربر (صفحه‌بندی)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<InAppNotificationDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<PagedResponse<InAppNotificationDto>>>> GetMyNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isRead = null,
            [FromQuery] string? type = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationService.GetMyNotificationsAsync(
                userId, page, pageSize, isRead, type);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تعداد اعلان‌های خوانده‌نشده (برای badge زنگوله)
        /// </summary>
        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<UnreadNotificationCountDto>>> GetUnreadCount()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationService.GetUnreadCountAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// علامت‌گذاری یک اعلان به‌عنوان خوانده‌شده
        /// </summary>
        [HttpPost("mark-read")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead([FromBody] MarkNotificationReadDto dto)
        {
            var invalid = InvalidModelStateResponse<bool>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationService.MarkAsReadAsync(userId, dto.NotificationId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// علامت‌گذاری همه اعلان‌ها به‌عنوان خوانده‌شده
        /// </summary>
        [HttpPost("mark-all-read")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAllAsRead()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationService.MarkAllAsReadAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم یک اعلان
        /// </summary>
        [HttpPost("delete/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationService.DeleteAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
