using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserRole;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت روابط کاربر-نقش (User Roles)
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت روابط کاربران و نقش‌ها می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - اختصاص نقش به کاربر
    /// - دریافت نقش‌های یک کاربر
    /// - دریافت کاربران یک نقش
    /// - فعال/غیرفعال کردن رابطه کاربر-نقش
    /// - حذف نرم و سخت رابطه
    /// 
    /// **نکات مهم:**
    /// - یک کاربر می‌تواند چندین نقش داشته باشد
    /// - یک نقش می‌تواند به چندین کاربر اختصاص داده شود
    /// - می‌توانید رابطه کاربر-نقش را فعال/غیرفعال کنید
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class UserRoleController : ControllerBase
    {
        private readonly IUserRoleService _userRoleService;

        public UserRoleController(IUserRoleService userRoleService)
        {
            _userRoleService = userRoleService;
        }

        /// <summary>
        /// استخراج خطاهای ModelState برای نمایش به کاربر
        /// </summary>
        private List<string> ExtractModelStateErrors()
        {
            return ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(error => 
                {
                    var errorMessage = error.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(errorMessage) && error.Exception != null)
                    {
                        errorMessage = error.Exception.Message;
                    }
                    return errorMessage;
                }))
                .ToList();
        }

        /// <summary>
        /// ایجاد رابطه کاربر-نقش (اختصاص نقش به کاربر)
        /// </summary>
        /// <param name="createUserRoleDto">اطلاعات رابطه شامل شناسه کاربر و نقش</param>
        /// <returns>پاسخ شامل اطلاعات رابطه ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای اختصاص یک نقش به یک کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کاربر و نقش باید وجود داشته باشند
        /// - رابطه باید منحصر به فرد باشد (یک کاربر نمی‌تواند دو بار همان نقش را داشته باشد)
        /// - رابطه ایجاد شده به صورت پیش‌فرض فعال است
        /// </remarks>
        /// <response code="200">رابطه کاربر-نقش با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یا نقش یافت نشد</response>
        /// <response code="409">این رابطه قبلاً ایجاد شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> CreateUserRole([FromBody] CreateUserRoleDto createUserRoleDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserRoleResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userRoleService.CreateUserRoleAsync(createUserRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات رابطه کاربر-نقش بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه رابطه کاربر-نقش</param>
        /// <returns>پاسخ شامل اطلاعات کامل رابطه</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک رابطه کاربر-نقش را بر اساس شناسه برمی‌گرداند.
        /// </remarks>
        /// <response code="200">اطلاعات رابطه با موفقیت برگردانده شد</response>
        /// <response code="404">رابطه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> GetUserRoleById(int id)
        {
            var result = await _userRoleService.GetUserRoleByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست نقش‌های یک کاربر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <returns>پاسخ شامل لیست نقش‌های کاربر</returns>
        /// <remarks>
        /// این endpoint لیست تمام نقش‌های اختصاص داده شده به یک کاربر را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - فقط نقش‌های فعال برگردانده می‌شوند
        /// - از این endpoint برای بررسی دسترسی‌های کاربر استفاده می‌شود
        /// </remarks>
        /// <response code="200">لیست نقش‌های کاربر با موفقیت برگردانده شد</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserRoleResponseDto>>>> GetUserRoles(int userId)
        {
            var result = await _userRoleService.GetUserRolesAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست کاربران یک نقش
        /// </summary>
        /// <param name="roleId">شناسه نقش</param>
        /// <returns>پاسخ شامل لیست کاربران دارای این نقش</returns>
        /// <remarks>
        /// این endpoint لیست تمام کاربرانی که یک نقش خاص دارند را برمی‌گرداند.
        /// 
        /// **استفاده:**
        /// برای بررسی اینکه کدام کاربران یک نقش خاص دارند استفاده می‌شود.
        /// </remarks>
        /// <response code="200">لیست کاربران نقش با موفقیت برگردانده شد</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("role/{roleId}")]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserRoleResponseDto>>>> GetRoleUsers(int roleId)
        {
            var result = await _userRoleService.GetRoleUsersAsync(roleId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست روابط کاربر-نقش با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="userId">فیلتر بر اساس شناسه کاربر (اختیاری)</param>
        /// <param name="roleId">فیلتر بر اساس شناسه نقش (اختیاری)</param>
        /// <param name="isActive">فیلتر بر اساس وضعیت فعال (true = فقط فعال، false = فقط غیرفعال، null = همه) (اختیاری)</param>
        /// <returns>پاسخ شامل لیست روابط کاربر-نقش و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام روابط کاربر-نقش را با امکان pagination و فیلتر برمی‌گرداند.
        /// 
        /// **فیلترها:**
        /// - می‌توانید بر اساس کاربر فیلتر کنید
        /// - می‌توانید بر اساس نقش فیلتر کنید
        /// - می‌توانید بر اساس وضعیت فعال/غیرفعال فیلتر کنید
        /// </remarks>
        /// <response code="200">لیست روابط با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleListResponseDto>>> GetUserRoles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? userId = null,
            [FromQuery] int? roleId = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _userRoleService.GetUserRolesListAsync(pageNumber, pageSize, userId, roleId, isActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی رابطه کاربر-نقش
        /// </summary>
        /// <param name="id">شناسه رابطه کاربر-نقش</param>
        /// <param name="updateUserRoleDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات رابطه به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک رابطه کاربر-نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - می‌توانید وضعیت فعال/غیرفعال را تغییر دهید
        /// </remarks>
        /// <response code="200">اطلاعات رابطه با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">رابطه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto updateUserRoleDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserRoleResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userRoleService.UpdateUserRoleAsync(id, updateUserRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم رابطه کاربر-نقش (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه رابطه کاربر-نقش</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف نرم یک رابطه کاربر-نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - رابطه از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted تنظیم می‌شود
        /// - رابطه حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - این عملیات قابل بازگشت است
        /// </remarks>
        /// <response code="200">رابطه با موفقیت حذف شد</response>
        /// <response code="404">رابطه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUserRole(int id)
        {
            var result = await _userRoleService.DeleteUserRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف سخت رابطه کاربر-نقش (Hard Delete)
        /// </summary>
        /// <param name="id">شناسه رابطه کاربر-نقش</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف کامل یک رابطه کاربر-نقش از دیتابیس استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - رابطه به طور کامل از دیتابیس حذف می‌شود
        /// - این عملیات قابل بازگشت نیست
        /// - توصیه می‌شود از حذف نرم استفاده کنید
        /// </remarks>
        /// <response code="200">رابطه با موفقیت حذف شد</response>
        /// <response code="404">رابطه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/hard-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> HardDeleteUserRole(int id)
        {
            var result = await _userRoleService.HardDeleteUserRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن رابطه کاربر-نقش
        /// </summary>
        /// <param name="id">شناسه رابطه کاربر-نقش</param>
        /// <param name="isActive">وضعیت جدید (true = فعال، false = غیرفعال)</param>
        /// <returns>پاسخ شامل اطلاعات رابطه با وضعیت جدید</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال یک رابطه کاربر-نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - رابطه غیرفعال همچنان در دیتابیس وجود دارد اما کاربر دسترسی ندارد
        /// - می‌توانید رابطه را موقتاً غیرفعال کنید بدون حذف
        /// - رابطه غیرفعال می‌تواند دوباره فعال شود
        /// </remarks>
        /// <response code="200">وضعیت رابطه با موفقیت تغییر کرد</response>
        /// <response code="404">رابطه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> ToggleUserRoleActiveStatus(
            int id,
            [FromBody] bool isActive)
        {
            var result = await _userRoleService.ToggleUserRoleActiveStatusAsync(id, isActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}

