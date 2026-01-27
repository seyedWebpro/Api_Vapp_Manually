using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Role;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت نقش‌ها (Roles)
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت نقش‌های سیستم می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت نقش‌ها
    /// - فعال/غیرفعال کردن نقش‌ها
    /// - حذف نرم و سخت نقش‌ها
    /// 
    /// **نکات مهم:**
    /// - نقش‌ها برای کنترل دسترسی کاربران استفاده می‌شوند
    /// - می‌توانید نقش‌ها را فعال/غیرفعال کنید
    /// - حذف نرم (Soft Delete) قابل بازگشت است
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
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
        /// ایجاد نقش جدید
        /// </summary>
        /// <param name="createRoleDto">اطلاعات نقش جدید شامل نام و توضیحات</param>
        /// <returns>پاسخ شامل اطلاعات نقش ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک نقش جدید در سیستم استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نام نقش باید منحصر به فرد باشد
        /// - نقش ایجاد شده به صورت پیش‌فرض فعال است
        /// </remarks>
        /// <response code="200">نقش با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="409">نقش با این نام قبلاً ایجاد شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> CreateRole([FromBody] CreateRoleDto createRoleDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<RoleResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _roleService.CreateRoleAsync(createRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات نقش بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه نقش</param>
        /// <returns>پاسخ شامل اطلاعات کامل نقش</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک نقش را بر اساس شناسه برمی‌گرداند.
        /// </remarks>
        /// <response code="200">اطلاعات نقش با موفقیت برگردانده شد</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> GetRoleById(int id)
        {
            var result = await _roleService.GetRoleByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست نقش‌ها با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="isActive">فیلتر بر اساس وضعیت فعال (true = فقط فعال، false = فقط غیرفعال، null = همه) (اختیاری)</param>
        /// <param name="isDeleted">فیلتر بر اساس وضعیت حذف (true = فقط حذف شده، false = فقط فعال، null = همه) (اختیاری)</param>
        /// <returns>پاسخ شامل لیست نقش‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام نقش‌های سیستم را با امکان pagination و فیلتر برمی‌گرداند.
        /// </remarks>
        /// <response code="200">لیست نقش‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleListResponseDto>>> GetRoles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            var result = await _roleService.GetRolesAsync(pageNumber, pageSize, isActive, isDeleted);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست نقش‌های فعال
        /// </summary>
        /// <returns>پاسخ شامل لیست نقش‌های فعال</returns>
        /// <remarks>
        /// این endpoint لیست تمام نقش‌های فعال سیستم را برمی‌گرداند.
        /// 
        /// **استفاده:**
        /// برای نمایش در لیست انتخاب نقش برای کاربران استفاده می‌شود.
        /// </remarks>
        /// <response code="200">لیست نقش‌های فعال با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("active")]
        [ProducesResponseType(typeof(ApiResponse<List<RoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<RoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<RoleResponseDto>>>> GetActiveRoles()
        {
            var result = await _roleService.GetActiveRolesAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی اطلاعات نقش
        /// </summary>
        /// <param name="id">شناسه نقش</param>
        /// <param name="updateRoleDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات نقش به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - نام نقش باید منحصر به فرد باشد
        /// </remarks>
        /// <response code="200">اطلاعات نقش با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="409">نقش با این نام قبلاً وجود دارد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> UpdateRole(int id, [FromBody] UpdateRoleDto updateRoleDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<RoleResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _roleService.UpdateRoleAsync(id, updateRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم نقش (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه نقش</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف نرم یک نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نقش از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted تنظیم می‌شود
        /// - نقش حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - این عملیات قابل بازگشت است
        /// </remarks>
        /// <response code="200">نقش با موفقیت حذف شد</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteRole(int id)
        {
            var result = await _roleService.DeleteRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف سخت نقش (Hard Delete) - حذف کامل از دیتابیس
        /// </summary>
        /// <param name="id">شناسه نقش</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف کامل یک نقش از دیتابیس استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نقش به طور کامل از دیتابیس حذف می‌شود
        /// - این عملیات قابل بازگشت نیست
        /// - قبل از حذف سخت، اطمینان حاصل کنید که نقش در جایی استفاده نمی‌شود
        /// - توصیه می‌شود از حذف نرم استفاده کنید
        /// </remarks>
        /// <response code="200">نقش با موفقیت حذف شد</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="409">نقش در حال استفاده است و قابل حذف نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/hard-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> HardDeleteRole(int id)
        {
            var result = await _roleService.HardDeleteRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن نقش
        /// </summary>
        /// <param name="id">شناسه نقش</param>
        /// <param name="isActive">وضعیت جدید (true = فعال، false = غیرفعال)</param>
        /// <returns>پاسخ شامل اطلاعات نقش با وضعیت جدید</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال یک نقش استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نقش غیرفعال نمی‌تواند به کاربران جدید اختصاص داده شود
        /// - نقش‌های قبلاً اختصاص داده شده همچنان معتبر هستند
        /// - نقش غیرفعال می‌تواند دوباره فعال شود
        /// </remarks>
        /// <response code="200">وضعیت نقش با موفقیت تغییر کرد</response>
        /// <response code="404">نقش یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> ToggleRoleActiveStatus(
            int id,
            [FromBody] bool isActive)
        {
            var result = await _roleService.ToggleRoleActiveStatusAsync(id, isActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}

