using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت مخاطبین و مشتریان
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت مخاطبین، ایمپورت/اکسپورت، مدیریت تصاویر و فایل‌های ضمیمه می‌باشد.
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند (به جز /all).
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;

        public ContactController(IContactService contactService, IConfiguration configuration, IUserRepository userRepository)
        {
            _contactService = contactService;
            _configuration = configuration;
            _userRepository = userRepository;
        }

        /// <summary>
        /// دریافت شناسه کاربر از JWT Token یا برگرداندن کاربر پیش‌فرض در حالت DisableAuth
        /// </summary>
        private async Task<int> GetCurrentUserIdAsync()
        {
            // ابتدا سعی می‌کنیم از Token بخوانیم (اگر وجود داشته باشد)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            // اگر Token وجود نداشت و DisableAuth فعال بود، از کاربر پیش‌فرض استفاده می‌کنیم
            var disableAuth = _configuration.GetValue<bool>("Development:DisableAuth", false);
            if (disableAuth)
            {
                var defaultUser = await _userRepository.GetOrCreateDefaultUserAsync();
                return defaultUser.Id;
            }

            // در غیر این صورت خطا می‌دهیم
            throw new UnauthorizedAccessException("شناسه کاربر معتبر نیست");
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
        /// ایجاد مخاطب جدید
        /// </summary>
        /// <param name="createDto">اطلاعات مخاطب جدید شامل شماره موبایل، نام، دفترچه و غیره</param>
        /// <returns>پاسخ شامل اطلاعات مخاطب ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد مخاطب جدید در دفترچه تلفن استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - اگر شماره موبایل تکراری باشد، مخاطب موجود برگردانده می‌شود (بدون خطا)
        /// - دفترچه تلفن (ContactNotebookId) باید وجود داشته باشد
        /// - برای آپلود عکس پروفایل از endpoint /{id}/upload-profile-image استفاده کنید
        /// - می‌توانید تگ‌ها را در زمان ایجاد یا بعداً با endpoint /{id}/tags اضافه کنید
        /// </remarks>
        /// <response code="200">مخاطب با موفقیت ایجاد شد یا قبلاً وجود داشت</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactResponseDto>>> CreateContact([FromBody] CreateContactDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ContactResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.CreateContactAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات مخاطب بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <returns>پاسخ شامل اطلاعات کامل مخاطب</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک مخاطب بر اساس شناسه استفاده می‌شود.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - اطلاعات شخصی (نام، شماره موبایل، برند و غیره)
        /// - تگ‌ها
        /// - تاریخ تولد و ازدواج
        /// - URL عکس پروفایل
        /// - فایل‌های ضمیمه
        /// </remarks>
        /// <response code="200">اطلاعات مخاطب با موفقیت برگردانده شد</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactResponseDto>>> GetContactById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.GetContactByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست تمام مخاطبین (بدون نیاز به احراز هویت)
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="searchTerm">عبارت جستجو برای فیلتر کردن بر اساس نام یا شماره موبایل (اختیاری)</param>
        /// <returns>پاسخ شامل لیست مخاطبین و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست تمام مخاطبین بدون نیاز به احراز هویت استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به توکن ندارد (AllowAnonymous)
        /// - تمام مخاطبین تمام کاربران را برمی‌گرداند
        /// - برای دریافت مخاطبین یک دفترچه خاص از endpoint /notebook/{notebookId} استفاده کنید
        /// </remarks>
        /// <response code="200">لیست مخاطبین با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("all")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactListResponseDto>>> GetAllContacts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var result = await _contactService.GetAllContactsAsync(pageNumber, pageSize, searchTerm);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست مخاطبین یک دفترچه با pagination و جستجو
        /// </summary>
        /// <param name="notebookId">شناسه دفترچه تلفن</param>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="searchTerm">عبارت جستجو برای فیلتر کردن بر اساس نام یا شماره موبایل (اختیاری)</param>
        /// <returns>پاسخ شامل لیست مخاطبین دفترچه و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست مخاطبین یک دفترچه خاص با امکان pagination و جستجو استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - دفترچه باید متعلق به کاربر فعلی باشد
        /// - جستجو بر اساس نام، نام خانوادگی یا شماره موبایل انجام می‌شود
        /// </remarks>
        /// <response code="200">لیست مخاطبین با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notebook/{notebookId}")]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactListResponseDto>>> GetContacts(
            int notebookId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.GetContactsAsync(notebookId, userId, pageNumber, pageSize, searchTerm);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی اطلاعات مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات مخاطب به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - اگر شماره موبایل تکراری باشد، شماره تغییر داده نمی‌شود (بدون خطا)
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">اطلاعات مخاطب با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<ContactResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactResponseDto>>> UpdateContact(int id, [FromBody] UpdateContactDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ContactResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.UpdateContactAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم مخاطب (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint مخاطب را به صورت نرم حذف می‌کند (IsDeleted = true).
        /// 
        /// **نکات مهم:**
        /// - مخاطب از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted برای آن تنظیم می‌شود
        /// - مخاطب حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - می‌توانید مخاطب حذف شده را دوباره بازیابی کنید
        /// </remarks>
        /// <response code="200">مخاطب با موفقیت حذف شد</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteContact(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.DeleteContactAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// انتقال مخاطب به دفترچه دیگر
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="transferDto">اطلاعات انتقال شامل شناسه دفترچه مبدا و مقصد</param>
        /// <returns>پاسخ شامل وضعیت انتقال</returns>
        /// <remarks>
        /// این endpoint برای انتقال یک مخاطب از یک دفترچه به دفترچه دیگر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - هر دو دفترچه باید متعلق به کاربر فعلی باشند
        /// - مخاطب باید در دفترچه مبدا وجود داشته باشد
        /// - پس از انتقال، مخاطب از دفترچه مبدا حذف و به دفترچه مقصد اضافه می‌شود
        /// </remarks>
        /// <response code="200">مخاطب با موفقیت انتقال یافت</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">مخاطب یا دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/transfer")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> TransferContact(
            int id,
            [FromBody] TransferContactDto transferDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<bool>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.TransferContactAsync(id, transferDto.FromNotebookId, transferDto.ToNotebookId, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایمپورت مخاطبین از فایل اکسل
        /// </summary>
        /// <param name="importDto">اطلاعات ایمپورت شامل فایل اکسل و شناسه دفترچه</param>
        /// <returns>پاسخ شامل نتیجه ایمپورت (تعداد موفق، ناموفق و خطاها)</returns>
        /// <remarks>
        /// این endpoint برای ایمپورت مخاطبین از فایل اکسل استفاده می‌شود.
        /// 
        /// **فرمت فایل اکسل:**
        /// فایل اکسل باید شامل ستون‌های زیر باشد:
        /// - **MobileNumber** / شماره موبایل (الزامی) - باید با 09 شروع شود و 11 رقم باشد
        /// - **FullName** / نام کامل (اختیاری) - یا ستون‌های Name/FirstName/LastName
        /// - **Brand** / برند (اختیاری)
        /// - **Tags** / برچسب‌ها (اختیاری) - با کاما جدا شوند: "تگ1,تگ2,تگ3"
        /// 
        /// **نکات مهم:**
        /// - فرمت فایل باید .xlsx یا .xls باشد
        /// - حداکثر حجم فایل: 10 مگابایت
        /// - شماره موبایل تکراری نادیده گرفته می‌شود
        /// - برای دریافت قالب از endpoint /import-excel-template استفاده کنید
        /// </remarks>
        /// <response code="200">ایمپورت با موفقیت انجام شد</response>
        /// <response code="400">فایل ارسال نشده یا فرمت نامعتبر است</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("import-excel")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ImportExcelResultDto>>> ImportFromExcel([FromForm] ImportContactsFromExcelDto importDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ImportExcelResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.ImportFromExcelAsync(userId, importDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایمپورت مخاطبین از لیست JSON
        /// </summary>
        /// <param name="importDto">اطلاعات ایمپورت شامل لیست مخاطبین و شناسه دفترچه</param>
        /// <returns>پاسخ شامل نتیجه ایمپورت (تعداد موفق، ناموفق و خطاها)</returns>
        /// <remarks>
        /// این endpoint برای ایمپورت مخاطبین از یک لیست JSON استفاده می‌شود.
        /// 
        /// **فرمت داده:**
        /// لیست مخاطبین باید شامل اطلاعات زیر باشد:
        /// - MobileNumber (الزامی)
        /// - FullName (اختیاری)
        /// - Brand (اختیاری)
        /// - Tags (اختیاری) - آرایه یا رشته با کاما
        /// 
        /// **نکات مهم:**
        /// - شماره موبایل تکراری نادیده گرفته می‌شود
        /// - برای ایمپورت تعداد زیاد از فایل اکسل استفاده کنید
        /// </remarks>
        /// <response code="200">ایمپورت با موفقیت انجام شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("import-list")]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ImportExcelResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ImportExcelResultDto>>> ImportFromList([FromBody] ImportContactsFromListDto importDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ImportExcelResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.ImportFromListAsync(userId, importDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت قالب فایل اکسل برای ایمپورت مخاطبین
        /// </summary>
        /// <returns>فایل اکسل قالب با ستون‌های مورد نیاز</returns>
        /// <remarks>
        /// این endpoint یک فایل اکسل قالب با ستون‌های مورد نیاز برای ایمپورت مخاطبین را برمی‌گرداند.
        /// 
        /// **ستون‌های قالب:**
        /// - MobileNumber (الزامی)
        /// - FullName (اختیاری)
        /// - Brand (اختیاری)
        /// - Tags (اختیاری)
        /// 
        /// **استفاده:**
        /// 1. این فایل را دانلود کنید
        /// 2. اطلاعات مخاطبین را در آن وارد کنید
        /// 3. فایل را از طریق endpoint /import-excel آپلود کنید
        /// </remarks>
        /// <response code="200">فایل قالب با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("import-excel-template")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetImportExcelTemplate()
        {
            var result = await _contactService.GetImportExcelTemplateAsync();
            
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return File(
                result.Data!.FileContent, 
                result.Data.ContentType, 
                result.Data.FileName);
        }

        /// <summary>
        /// اکسپورت مخاطبین یک دفترچه به فایل اکسل (با صفحه‌بندی)
        /// </summary>
        /// <param name="notebookId">شناسه دفترچه تلفن</param>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 100، حداکثر: 1000)</param>
        /// <returns>فایل اکسل شامل اطلاعات مخاطبین</returns>
        /// <remarks>
        /// این endpoint مخاطبین یک دفترچه را به فایل اکسل اکسپورت می‌کند.
        /// 
        /// **ستون‌های فایل اکسل:**
        /// - ردیف
        /// - شماره موبایل
        /// - نام
        /// - نام خانوادگی
        /// - برند
        /// - برچسب‌ها
        /// - تاریخ تولد
        /// - تاریخ ازدواج
        /// - تاریخ ایجاد
        /// 
        /// **نکات مهم:**
        /// - برای اکسپورت تمام مخاطبین از endpoint /export-excel-all استفاده کنید
        /// - حداکثر 1000 مخاطب در هر درخواست اکسپورت می‌شود
        /// </remarks>
        /// <response code="200">فایل اکسل با موفقیت برگردانده شد</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notebook/{notebookId}/export-excel")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ExportToExcel(
            int notebookId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.ExportToExcelAsync(notebookId, userId, pageNumber, pageSize);
            
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            // برگرداندن فایل اکسل
            return File(
                result.Data!.FileContent, 
                result.Data.ContentType, 
                result.Data.FileName);
        }

        /// <summary>
        /// اکسپورت تمام مخاطبین یک دفترچه به فایل اکسل (بدون صفحه‌بندی)
        /// </summary>
        /// <param name="notebookId">شناسه دفترچه تلفن</param>
        /// <returns>فایل اکسل شامل تمام اطلاعات مخاطبین</returns>
        /// <remarks>
        /// این endpoint تمام مخاطبین یک دفترچه را بدون محدودیت تعداد به فایل اکسل اکسپورت می‌کند.
        /// 
        /// **نکات مهم:**
        /// - تمام مخاطبین دفترچه اکسپورت می‌شوند (بدون محدودیت)
        /// - برای دفترچه‌های بزرگ ممکن است زمان بیشتری طول بکشد
        /// - فرمت فایل مشابه endpoint /export-excel است
        /// </remarks>
        /// <response code="200">فایل اکسل با موفقیت برگردانده شد</response>
        /// <response code="404">دفترچه تلفن یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notebook/{notebookId}/export-excel-all")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ExportAllToExcel(int notebookId)
        {
            var userId = await GetCurrentUserIdAsync();
            // استفاده از pageSize بزرگ برای دریافت همه مخاطبین
            var result = await _contactService.ExportToExcelAsync(notebookId, userId, 1, 100000);
            
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            // برگرداندن فایل اکسل
            return File(
                result.Data!.FileContent, 
                result.Data.ContentType, 
                result.Data.FileName);
        }

        /// <summary>
        /// آپلود عکس پروفایل مخاطب (بدون نیاز به احراز هویت)
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="dto">فایل تصویر پروفایل</param>
        /// <returns>پاسخ شامل URL عکس پروفایل آپلود شده</returns>
        /// <remarks>
        /// این endpoint برای آپلود عکس پروفایل مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به احراز هویت ندارد (AllowAnonymous)
        /// - فرمت‌های مجاز: JPG, JPEG, PNG, GIF
        /// - حداکثر حجم فایل: 5 مگابایت
        /// - نسبت تصویر توصیه می‌شود 1:1 (مربع) باشد
        /// - در صورت آپلود عکس جدید، عکس قبلی جایگزین می‌شود
        /// </remarks>
        /// <response code="200">عکس پروفایل با موفقیت آپلود شد</response>
        /// <response code="400">فایل ارسال نشده یا فرمت/حجم نامعتبر است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/upload-profile-image")]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<string>>> UploadProfileImage(int id, [FromForm] UploadProfileImageDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<string>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var result = await _contactService.UploadProfileImageAsync(id, dto.ImageFile);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف عکس پروفایل مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف عکس پروفایل مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - پس از حذف، عکس پیش‌فرض نمایش داده می‌شود
        /// - این عملیات قابل بازگشت نیست
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">عکس پروفایل با موفقیت حذف شد</response>
        /// <response code="404">مخاطب یا عکس پروفایل یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete-profile-image")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProfileImage(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.DeleteProfileImageAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// آپلود فایل‌های ضمیمه برای مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="dto">فایل‌های ضمیمه (می‌تواند چند فایل باشد)</param>
        /// <returns>پاسخ شامل لیست URL فایل‌های آپلود شده</returns>
        /// <remarks>
        /// این endpoint برای آپلود یک یا چند فایل ضمیمه برای مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - می‌توانید چند فایل را همزمان آپلود کنید
        /// - فرمت‌های مجاز: تمام فرمت‌ها
        /// - حداکثر حجم هر فایل: 10 مگابایت
        /// - حداکثر تعداد فایل در هر درخواست: 10 فایل
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">فایل‌های ضمیمه با موفقیت آپلود شدند</response>
        /// <response code="400">فایل‌ها ارسال نشده یا فرمت/حجم نامعتبر است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/upload-attachments")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<string>>>> UploadAttachments(int id, [FromForm] UploadAttachmentsDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<List<string>>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.UploadAttachmentFilesAsync(id, userId, dto.Files);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف فایل ضمیمه مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="filePath">مسیر فایل ضمیمه (URL کامل فایل)</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک فایل ضمیمه از مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - مسیر فایل باید URL کامل فایل باشد (از endpoint /attachments دریافت کنید)
        /// - این عملیات قابل بازگشت نیست
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">فایل ضمیمه با موفقیت حذف شد</response>
        /// <response code="400">مسیر فایل ارسال نشده است</response>
        /// <response code="404">مخاطب یا فایل یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete-attachment")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAttachment(int id, [FromBody] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return StatusCode(400, ApiResponse<bool>.BadRequest("مسیر فایل ارسال نشده است"));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.DeleteAttachmentFileAsync(id, userId, filePath);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست فایل‌های ضمیمه مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <returns>پاسخ شامل لیست URL فایل‌های ضمیمه</returns>
        /// <remarks>
        /// این endpoint لیست تمام فایل‌های ضمیمه یک مخاطب را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - لیست شامل URL کامل تمام فایل‌های ضمیمه است
        /// - می‌توانید از این URL ها برای دانلود یا حذف فایل‌ها استفاده کنید
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">لیست فایل‌های ضمیمه با موفقیت برگردانده شد</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}/attachments")]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAttachments(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.GetAttachmentFilesAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اختصاص تگ‌ها به مخاطب
        /// </summary>
        /// <param name="id">شناسه مخاطب</param>
        /// <param name="assignDto">اطلاعات شامل لیست تگ‌ها</param>
        /// <returns>پاسخ شامل وضعیت اختصاص تگ‌ها</returns>
        /// <remarks>
        /// این endpoint برای اختصاص تگ‌ها به یک مخاطب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - تگ‌های موجود جایگزین می‌شوند (نه اضافه)
        /// - اگر تگی وجود نداشته باشد، ایجاد می‌شود
        /// - می‌توانید لیست خالی ارسال کنید تا تمام تگ‌ها حذف شوند
        /// - مخاطب باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">تگ‌ها با موفقیت اختصاص یافتند</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/tags")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> AssignTagsToContact(int id, [FromBody] AssignTagsToContactDto assignDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<bool>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _contactService.AssignTagsToContactAsync(id, userId, assignDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست دفترچه‌های تلفن یک کاربر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <returns>پاسخ شامل لیست دفترچه‌های تلفن کاربر</returns>
        /// <remarks>
        /// این endpoint لیست تمام دفترچه‌های تلفن یک کاربر را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - لیست شامل تمام دفترچه‌های فعال و غیرفعال است
        /// - برای دریافت فقط دفترچه‌های فعال، از ContactNotebookController استفاده کنید
        /// </remarks>
        /// <response code="200">لیست دفترچه‌ها با موفقیت برگردانده شد</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("user/{userId}/notebooks")]
        [ProducesResponseType(typeof(ApiResponse<List<ContactNotebookResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<ContactNotebookResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<ContactNotebookResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<ContactNotebookResponseDto>>>> GetUserNotebooks(int userId)
        {
            var result = await _contactService.GetUserNotebooksAsync(userId);
            return StatusCode(result.StatusCode, result);
        }
    }
}


