using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت کاربران
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت کاربران، پروفایل، تنظیمات اعلان‌ها و مدیریت تصاویر می‌باشد.
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class UserController : VappControllerBase
    {
        private readonly IUserService _userService;
        private readonly INotificationSettingsService _notificationSettingsService;

        public UserController(
            IUserService userService, 
            IConfiguration configuration, 
            IUserRepository userRepository,
            INotificationSettingsService notificationSettingsService)
            : base(configuration, userRepository)
        {
            _userService = userService;
            _notificationSettingsService = notificationSettingsService;
        }

        /// <summary>
        /// ایجاد کاربر جدید
        /// </summary>
        /// <param name="createUserDto">اطلاعات کاربر جدید شامل شماره موبایل، نام، کد ملی و غیره</param>
        /// <returns>پاسخ شامل اطلاعات کاربر ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد کاربر جدید توسط مدیر سیستم استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - شماره موبایل باید منحصر به فرد باشد
        /// - کد ملی باید 10 رقم باشد
        /// - کاربر ایجاد شده به صورت پیش‌فرض فعال است
        /// </remarks>
        /// <response code="200">کاربر با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="409">شماره موبایل یا کد ملی قبلاً ثبت شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userService.CreateUserAsync(createUserDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کاربر بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <returns>پاسخ شامل اطلاعات کاربر</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک کاربر بر اساس شناسه استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کاربر باید وجود داشته باشد
        /// - کاربر حذف شده (IsDeleted = true) برگردانده نمی‌شود
        /// </remarks>
        /// <response code="200">اطلاعات کاربر با موفقیت برگردانده شد</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetUserById(int id)
        {
            var result = await _userService.GetUserByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست کاربران با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="isActive">فیلتر بر اساس وضعیت فعال/غیرفعال (اختیاری)</param>
        /// <param name="isDeleted">فیلتر بر اساس وضعیت حذف شده (اختیاری)</param>
        /// <returns>پاسخ شامل لیست کاربران و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست کاربران با امکان pagination و فیلتر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - pageSize نباید بیشتر از 100 باشد
        /// - در صورت عدم ارسال فیلترها، تمام کاربران برگردانده می‌شوند
        /// </remarks>
        /// <response code="200">لیست کاربران با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<UserListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserListResponseDto>>> GetUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            var result = await _userService.GetUsersAsync(pageNumber, pageSize, isActive, isDeleted);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی اطلاعات کاربر
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <param name="updateUserDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات کاربر به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - شماره موبایل و کد ملی باید منحصر به فرد باشند
        /// </remarks>
        /// <response code="200">اطلاعات کاربر با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="409">شماره موبایل یا کد ملی قبلاً ثبت شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userService.UpdateUserAsync(id, updateUserDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم کاربر (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint کاربر را به صورت نرم حذف می‌کند (IsDeleted = true).
        /// 
        /// **نکات مهم:**
        /// - کاربر از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted برای آن تنظیم می‌شود
        /// - کاربر حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - برای حذف کامل از endpoint /hard-delete استفاده کنید
        /// </remarks>
        /// <response code="200">کاربر با موفقیت حذف شد</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
        {
            var result = await _userService.DeleteUserAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف سخت کاربر (Hard Delete) - حذف کامل از دیتابیس
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint کاربر را به صورت کامل از دیتابیس حذف می‌کند.
        /// 
        /// **⚠️ هشدار:**
        /// - این عملیات غیرقابل بازگشت است
        /// - تمام اطلاعات کاربر به صورت کامل حذف می‌شود
        /// - فقط در موارد خاص و با احتیاط استفاده شود
        /// </remarks>
        /// <response code="200">کاربر با موفقیت از دیتابیس حذف شد</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/hard-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> HardDeleteUser(int id)
        {
            var result = await _userService.HardDeleteUserAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// بن کردن یا رفع بن کاربر
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <param name="banUserDto">اطلاعات بن شامل دلیل و وضعیت</param>
        /// <returns>پاسخ شامل اطلاعات کاربر به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای بن کردن یا رفع بن کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کاربر بن شده نمی‌تواند وارد سیستم شود
        /// - می‌توانید دلیل بن را ثبت کنید
        /// </remarks>
        /// <response code="200">وضعیت بن کاربر با موفقیت تغییر کرد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/ban")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> BanUser(int id, [FromBody] BanUserDto banUserDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userService.BanUserAsync(id, banUserDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن کاربر
        /// </summary>
        /// <param name="id">شناسه کاربر</param>
        /// <param name="toggleActiveDto">وضعیت جدید (فعال/غیرفعال)</param>
        /// <returns>پاسخ شامل اطلاعات کاربر به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کاربر غیرفعال نمی‌تواند وارد سیستم شود
        /// - این با بن کردن متفاوت است (کاربر غیرفعال می‌تواند دوباره فعال شود)
        /// </remarks>
        /// <response code="200">وضعیت کاربر با موفقیت تغییر کرد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> ToggleUserActiveStatus(
            int id,
            [FromBody] ToggleActiveDto toggleActiveDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _userService.ToggleUserActiveStatusAsync(id, toggleActiveDto.IsActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کاربر فعلی از توکن
        /// </summary>
        /// <returns>پاسخ شامل اطلاعات کاربر فعلی</returns>
        /// <remarks>
        /// این endpoint توکن را از header Authorization می‌خواند و اطلاعات کاربر فعلی را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - نیاز به احراز هویت دارد
        /// - اطلاعات کاربر از روی JWT Token استخراج می‌شود
        /// - برای دریافت اطلاعات کامل‌تر از endpoint /profile استفاده کنید
        /// </remarks>
        /// <response code="200">اطلاعات کاربر با موفقیت برگردانده شد</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetCurrentUser()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userService.GetUserByIdAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #region Profile Management

        /// <summary>
        /// دریافت اطلاعات کامل پروفایل کاربر (شامل موجودی کیف پول)
        /// </summary>
        /// <returns>پاسخ شامل اطلاعات کامل پروفایل کاربر و موجودی کیف پول</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل پروفایل کاربر فعلی را برمی‌گرداند که شامل:
        /// - اطلاعات شخصی کاربر
        /// - موجودی کیف پول
        /// - آمار و اطلاعات تکمیلی
        /// 
        /// **نکات مهم:**
        /// - نیاز به احراز هویت دارد
        /// - اطلاعات کاربر از روی JWT Token استخراج می‌شود
        /// </remarks>
        /// <response code="200">اطلاعات پروفایل با موفقیت برگردانده شد</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("profile")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userService.GetUserProfileAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی پروفایل کاربر
        /// </summary>
        /// <param name="updateDto">اطلاعات به‌روزرسانی پروفایل</param>
        /// <returns>پاسخ شامل اطلاعات پروفایل به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات پروفایل کاربر فعلی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - شماره موبایل و کد ملی باید منحصر به فرد باشند
        /// - برای آپلود عکس پروفایل از endpoint /profile/upload-image استفاده کنید
        /// </remarks>
        /// <response code="200">پروفایل با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="409">شماره موبایل یا کد ملی قبلاً ثبت شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("profile/update")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile([FromBody] UpdateUserProfileDto updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<UserProfileDto>.BadRequest("اطلاعات به‌روزرسانی ارسال نشده است"));
            }

            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserProfileDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _userService.UpdateUserProfileAsync(userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// آپلود عکس پروفایل کاربر
        /// </summary>
        /// <param name="dto">فایل تصویر پروفایل</param>
        /// <returns>پاسخ شامل URL عکس پروفایل آپلود شده</returns>
        /// <remarks>
        /// این endpoint برای آپلود عکس پروفایل کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فرمت‌های مجاز: JPG, JPEG, PNG, GIF
        /// - حداکثر حجم فایل: 5 مگابایت
        /// - نسبت تصویر توصیه می‌شود 1:1 (مربع) باشد
        /// - در صورت آپلود عکس جدید، عکس قبلی جایگزین می‌شود
        /// </remarks>
        /// <response code="200">عکس پروفایل با موفقیت آپلود شد</response>
        /// <response code="400">فایل ارسال نشده یا فرمت/حجم نامعتبر است</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("profile/upload-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<string>>> UploadProfileImage([FromForm] UploadProfileImageDto dto)
        {
            if (dto == null || dto.ImageFile == null)
            {
                return StatusCode(400, ApiResponse<string>.BadRequest("فایل عکس ارسال نشده است. لطفاً یک فایل تصویری انتخاب کنید"));
            }

            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<string>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _userService.UploadProfileImageAsync(userId, dto.ImageFile);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف عکس پروفایل کاربر
        /// </summary>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف عکس پروفایل کاربر فعلی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - پس از حذف، عکس پیش‌فرض نمایش داده می‌شود
        /// - این عملیات قابل بازگشت نیست
        /// </remarks>
        /// <response code="200">عکس پروفایل با موفقیت حذف شد</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="404">عکس پروفایلی برای حذف یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("profile/delete-image")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProfileImage()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userService.DeleteProfileImageAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Notification Settings

        /// <summary>
        /// دریافت تنظیمات اعلان‌های کاربر
        /// </summary>
        /// <returns>پاسخ شامل تنظیمات اعلان‌های کاربر</returns>
        /// <remarks>
        /// این endpoint تنظیمات اعلان‌های کاربر فعلی را برمی‌گرداند.
        /// 
        /// **تنظیمات شامل:**
        /// - اعلان‌های پیامک
        /// - اعلان‌های ایمیل
        /// - اعلان‌های Push
        /// - تنظیمات زمان‌بندی
        /// </remarks>
        /// <response code="200">تنظیمات اعلان‌ها با موفقیت برگردانده شد</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="404">تنظیمات یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notification-settings")]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<NotificationSettingsDto>>> GetNotificationSettings()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationSettingsService.GetSettingsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی تنظیمات اعلان‌های کاربر
        /// </summary>
        /// <param name="settingsDto">تنظیمات جدید اعلان‌ها</param>
        /// <returns>پاسخ شامل تنظیمات به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی تنظیمات اعلان‌های کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - تمام تنظیمات ارسال شده به‌روزرسانی می‌شوند
        /// - در صورت عدم وجود تنظیمات، تنظیمات جدید ایجاد می‌شود
        /// </remarks>
        /// <response code="200">تنظیمات اعلان‌ها با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("notification-settings/update")]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<NotificationSettingsDto>>> UpdateNotificationSettings([FromBody] NotificationSettingsDto settingsDto)
        {
            if (settingsDto == null)
            {
                return StatusCode(400, ApiResponse<NotificationSettingsDto>.BadRequest("تنظیمات ارسال نشده است"));
            }

            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<NotificationSettingsDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _notificationSettingsService.UpdateSettingsAsync(userId, settingsDto);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}

