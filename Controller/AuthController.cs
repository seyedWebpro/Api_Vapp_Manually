using Api_Vapp.DTOs.Auth;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر احراز هویت و مدیریت کاربران
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به احراز هویت، ثبت‌نام، ورود، فراموشی رمز عبور و مدیریت توکن‌ها می‌باشد.
    /// سیستم احراز هویت بر اساس OTP (One-Time Password) و JWT Token کار می‌کند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : VappControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService, IUserRepository userRepository, IConfiguration configuration)
            : base(configuration, userRepository)
        {
            _authService = authService;
        }

        /// <summary>
        /// دریافت IP Address کلاینت
        /// </summary>
        private string? GetClientIpAddress()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            
            // بررسی X-Forwarded-For header برای پروکسی‌ها
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
            }
            
            return ipAddress;
        }

        /// <summary>
        /// ثبت نام کاربر جدید - ارسال کد تایید OTP
        /// </summary>
        /// <param name="registerDto">اطلاعات ثبت‌نام شامل شماره موبایل، نام، کد ملی و غیره</param>
        /// <returns>پاسخ شامل وضعیت ارسال OTP و زمان انقضا</returns>
        /// <remarks>
        /// این endpoint برای ثبت‌نام کاربر جدید استفاده می‌شود. پس از ارسال درخواست، یک کد OTP به شماره موبایل کاربر ارسال می‌شود.
        /// 
        /// **فرآیند ثبت‌نام:**
        /// 1. ارسال درخواست ثبت‌نام به این endpoint
        /// 2. دریافت کد OTP از طریق پیامک
        /// 3. تایید کد OTP با استفاده از endpoint /verify-registration
        /// 
        /// **نکات مهم:**
        /// - شماره موبایل باید با فرمت صحیح (09xxxxxxxxx) ارسال شود
        /// - کد ملی باید 10 رقم باشد
        /// - کد OTP به مدت 5 دقیقه معتبر است
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="409">شماره موبایل قبلاً ثبت شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.RegisterAsync(registerDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تایید کد OTP و تکمیل ثبت نام
        /// </summary>
        /// <param name="verifyOtpDto">اطلاعات تایید شامل شماره موبایل و کد OTP</param>
        /// <returns>پاسخ شامل Access Token و Refresh Token در صورت موفقیت</returns>
        /// <remarks>
        /// این endpoint برای تایید کد OTP ارسال شده در مرحله ثبت‌نام استفاده می‌شود.
        /// 
        /// **در صورت موفقیت:**
        /// - حساب کاربری فعال می‌شود
        /// - Access Token و Refresh Token برگردانده می‌شود
        /// - کاربر می‌تواند از این توکن‌ها برای دسترسی به API استفاده کند
        /// 
        /// **نکات مهم:**
        /// - کد OTP فقط 5 دقیقه معتبر است
        /// - هر کد OTP فقط یک بار قابل استفاده است
        /// - در صورت اشتباه بودن کد، می‌توانید از endpoint /resend-registration-otp استفاده کنید
        /// </remarks>
        /// <response code="200">ثبت‌نام با موفقیت انجام شد و توکن‌ها برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا کد OTP اشتباه است</response>
        /// <response code="401">کد OTP منقضی شده یا نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("verify-registration")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthResponseDto>> VerifyRegistration([FromBody] VerifyOtpDto verifyOtpDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    Errors = errors
                });
            }

            var result = await _authService.VerifyRegistrationOtpAsync(verifyOtpDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال مجدد کد تایید OTP برای ثبت نام
        /// </summary>
        /// <param name="loginDto">اطلاعات شامل شماره موبایل</param>
        /// <returns>پاسخ شامل وضعیت ارسال مجدد OTP</returns>
        /// <remarks>
        /// این endpoint برای ارسال مجدد کد OTP در صورت عدم دریافت یا منقضی شدن کد قبلی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط برای کاربرانی که هنوز ثبت‌نام خود را تکمیل نکرده‌اند قابل استفاده است
        /// - محدودیت نرخ ارسال: حداکثر 3 بار در 10 دقیقه
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="429">تعداد درخواست‌ها بیش از حد مجاز است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("resend-registration-otp")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> ResendRegistrationOtp([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.ResendRegistrationOtpAsync(loginDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ورود کاربر - ارسال کد تایید OTP
        /// </summary>
        /// <param name="loginDto">اطلاعات ورود شامل شماره موبایل</param>
        /// <returns>پاسخ شامل وضعیت ارسال OTP و زمان انقضا</returns>
        /// <remarks>
        /// این endpoint برای ورود کاربران ثبت‌نام شده استفاده می‌شود. پس از ارسال درخواست، یک کد OTP به شماره موبایل کاربر ارسال می‌شود.
        /// 
        /// **فرآیند ورود:**
        /// 1. ارسال درخواست ورود به این endpoint
        /// 2. دریافت کد OTP از طریق پیامک
        /// 3. تایید کد OTP با استفاده از endpoint /verify-login
        /// 
        /// **نکات مهم:**
        /// - شماره موبایل باید قبلاً ثبت‌نام شده باشد
        /// - کد OTP به مدت 5 دقیقه معتبر است
        /// - در صورت عدم دریافت کد، می‌توانید از endpoint /resend-login-otp استفاده کنید
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر با این شماره موبایل یافت نشد</response>
        /// <response code="403">حساب کاربری غیرفعال یا مسدود شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.LoginAsync(loginDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تایید کد OTP و ورود کاربر
        /// </summary>
        /// <param name="verifyOtpDto">اطلاعات تایید شامل شماره موبایل و کد OTP</param>
        /// <returns>پاسخ شامل Access Token و Refresh Token در صورت موفقیت</returns>
        /// <remarks>
        /// این endpoint برای تایید کد OTP ارسال شده در مرحله ورود استفاده می‌شود.
        /// 
        /// **در صورت موفقیت:**
        /// - Access Token و Refresh Token برگردانده می‌شود
        /// - تاریخ آخرین ورود به‌روزرسانی می‌شود
        /// - کاربر می‌تواند از این توکن‌ها برای دسترسی به API استفاده کند
        /// 
        /// **نکات مهم:**
        /// - کد OTP فقط 5 دقیقه معتبر است
        /// - هر کد OTP فقط یک بار قابل استفاده است
        /// - Access Token به مدت 60 دقیقه معتبر است
        /// - Refresh Token به مدت 1 روز معتبر است
        /// </remarks>
        /// <response code="200">ورود با موفقیت انجام شد و توکن‌ها برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا کد OTP اشتباه است</response>
        /// <response code="401">کد OTP منقضی شده یا نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("verify-login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthResponseDto>> VerifyLogin([FromBody] VerifyOtpDto verifyOtpDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    Errors = errors
                });
            }

            var result = await _authService.VerifyLoginOtpAsync(verifyOtpDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ورود به پنل مدیریت — ارسال OTP (فقط کاربران دارای نقش Admin)
        /// </summary>
        [HttpPost("admin/login")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<SendOtpResponseDto>> AdminLogin([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.LoginAsync(loginDto, GetClientIpAddress(), requireAdminPanelAccess: true);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تایید OTP ورود به پنل مدیریت (فقط کاربران دارای نقش Admin)
        /// </summary>
        [HttpPost("admin/verify-login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AuthResponseDto>> AdminVerifyLogin([FromBody] VerifyOtpDto verifyOtpDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    Errors = errors
                });
            }

            var result = await _authService.VerifyLoginOtpAsync(verifyOtpDto, GetClientIpAddress(), requireAdminPanelAccess: true);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال مجدد OTP ورود به پنل مدیریت (فقط کاربران دارای نقش Admin)
        /// </summary>
        [HttpPost("admin/resend-login-otp")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SendOtpResponseDto>> AdminResendLoginOtp([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.ResendLoginOtpAsync(loginDto, GetClientIpAddress(), requireAdminPanelAccess: true);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال مجدد کد تایید OTP برای ورود
        /// </summary>
        /// <param name="loginDto">اطلاعات شامل شماره موبایل</param>
        /// <returns>پاسخ شامل وضعیت ارسال مجدد OTP</returns>
        /// <remarks>
        /// این endpoint برای ارسال مجدد کد OTP در صورت عدم دریافت یا منقضی شدن کد قبلی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط برای کاربران ثبت‌نام شده قابل استفاده است
        /// - محدودیت نرخ ارسال: حداکثر 3 بار در 10 دقیقه
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="429">تعداد درخواست‌ها بیش از حد مجاز است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("resend-login-otp")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> ResendLoginOtp([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.ResendLoginOtpAsync(loginDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فراموشی رمز عبور - ارسال کد تایید OTP
        /// </summary>
        /// <param name="forgotPasswordDto">اطلاعات شامل شماره موبایل</param>
        /// <returns>پاسخ شامل وضعیت ارسال OTP</returns>
        /// <remarks>
        /// این endpoint برای شروع فرآیند بازیابی رمز عبور استفاده می‌شود. پس از ارسال درخواست، یک کد OTP به شماره موبایل کاربر ارسال می‌شود.
        /// 
        /// **فرآیند بازیابی رمز عبور:**
        /// 1. ارسال درخواست به این endpoint
        /// 2. دریافت کد OTP از طریق پیامک
        /// 3. تایید کد OTP و تنظیم رمز جدید با استفاده از endpoint /reset-password
        /// 
        /// **نکات مهم:**
        /// - شماره موبایل باید قبلاً ثبت‌نام شده باشد
        /// - کد OTP به مدت 5 دقیقه معتبر است
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر با این شماره موبایل یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("forgot-password")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.ForgotPasswordAsync(forgotPasswordDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر رمز عبور با کد تایید OTP
        /// </summary>
        /// <param name="resetPasswordDto">اطلاعات شامل شماره موبایل، کد OTP و رمز عبور جدید</param>
        /// <returns>پاسخ شامل Access Token و Refresh Token در صورت موفقیت</returns>
        /// <remarks>
        /// این endpoint برای تایید کد OTP و تنظیم رمز عبور جدید استفاده می‌شود.
        /// 
        /// **در صورت موفقیت:**
        /// - رمز عبور کاربر به‌روزرسانی می‌شود
        /// - Access Token و Refresh Token برگردانده می‌شود
        /// - کاربر می‌تواند از این توکن‌ها برای دسترسی به API استفاده کند
        /// 
        /// **نکات مهم:**
        /// - کد OTP باید از endpoint /forgot-password دریافت شده باشد
        /// - کد OTP فقط 5 دقیقه معتبر است
        /// - رمز عبور جدید باید حداقل 6 کاراکتر باشد
        /// </remarks>
        /// <response code="200">رمز عبور با موفقیت تغییر کرد و توکن‌ها برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا کد OTP اشتباه است</response>
        /// <response code="401">کد OTP منقضی شده یا نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthResponseDto>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    Errors = errors
                });
            }

            var result = await _authService.ResetPasswordAsync(resetPasswordDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال مجدد کد تایید OTP برای فراموشی رمز عبور
        /// </summary>
        /// <param name="loginDto">اطلاعات شامل شماره موبایل</param>
        /// <returns>پاسخ شامل وضعیت ارسال مجدد OTP</returns>
        /// <remarks>
        /// این endpoint برای ارسال مجدد کد OTP در صورت عدم دریافت یا منقضی شدن کد قبلی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط برای کاربران ثبت‌نام شده قابل استفاده است
        /// - محدودیت نرخ ارسال: حداکثر 3 بار در 10 دقیقه
        /// </remarks>
        /// <response code="200">کد OTP با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="429">تعداد درخواست‌ها بیش از حد مجاز است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("resend-forgot-password-otp")]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SendOtpResponseDto>> ResendForgotPasswordOtp([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new SendOtpResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    ExpiresInSeconds = 0,
                    Errors = errors
                });
            }

            var result = await _authService.ResendForgotPasswordOtpAsync(loginDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی Access Token با استفاده از Refresh Token
        /// </summary>
        /// <param name="refreshTokenDto">اطلاعات شامل Refresh Token</param>
        /// <returns>پاسخ شامل Access Token و Refresh Token جدید</returns>
        /// <remarks>
        /// این endpoint برای تمدید Access Token منقضی شده با استفاده از Refresh Token استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - Refresh Token باید معتبر و منقضی نشده باشد
        /// - پس از استفاده، Refresh Token قدیمی باطل می‌شود و یک Refresh Token جدید برگردانده می‌شود
        /// - Access Token جدید به مدت 60 دقیقه معتبر است
        /// - Refresh Token جدید به مدت 1 روز معتبر است
        /// 
        /// **استفاده:**
        /// زمانی که Access Token منقضی می‌شود (کد 401)، می‌توانید از این endpoint برای دریافت توکن جدید استفاده کنید.
        /// </remarks>
        /// <response code="200">توکن‌های جدید با موفقیت برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">Refresh Token نامعتبر یا منقضی شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "داده‌های ورودی نامعتبر است",
                    Errors = errors
                });
            }

            var result = await _authService.RefreshTokenAsync(refreshTokenDto, GetClientIpAddress());
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// خروج از سیستم - لغو تمام توکن‌های کاربر
        /// </summary>
        /// <returns>پاسخ شامل وضعیت خروج از سیستم</returns>
        /// <remarks>
        /// این endpoint تمام Refresh Token های کاربر را لغو می‌کند و Access Token فعلی را به blacklist اضافه می‌کند.
        /// 
        /// **نکات مهم:**
        /// - نیاز به احراز هویت دارد (ارسال Access Token در header)
        /// - پس از خروج، تمام توکن‌های کاربر باطل می‌شوند
        /// - برای ورود مجدد باید از endpoint /login استفاده کنید
        /// </remarks>
        /// <response code="200">خروج از سیستم با موفقیت انجام شد</response>
        /// <response code="401">توکن معتبر نیست یا منقضی شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(LogoutResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(LogoutResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(LogoutResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LogoutResponseDto>> Logout()
        {
            var userId = await GetCurrentUserIdAsync();

            var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            var result = await _authService.LogoutAsync(userId, jti, GetClientIpAddress());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کاربر از توکن JWT
        /// </summary>
        /// <param name="tokenDto">اطلاعات شامل JWT Token</param>
        /// <returns>پاسخ شامل اطلاعات کاربر</returns>
        /// <remarks>
        /// این endpoint توکن JWT را از body دریافت می‌کند، اعتبارسنجی می‌کند و اطلاعات کاربر را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - توکن می‌تواند با یا بدون prefix "Bearer " ارسال شود
        /// - توکن باید معتبر و منقضی نشده باشد
        /// - این endpoint برای احراز هویت در سمت کلاینت استفاده می‌شود
        /// 
        /// **استفاده:**
        /// زمانی که می‌خواهید اطلاعات کاربر را از روی توکن ذخیره شده در کلاینت دریافت کنید.
        /// </remarks>
        /// <response code="200">اطلاعات کاربر با موفقیت برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">توکن نامعتبر یا منقضی شده است</response>
        /// <response code="404">کاربر یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("get-user-by-token")]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetUserByToken([FromBody] GetUserByTokenDto tokenDto)
        {
            if (!ModelState.IsValid)
            {
                 var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<UserResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            try
            {
                var token = tokenDto.Token.Trim();
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(7).Trim();
                }

                var secretKey = Configuration["Jwt:Secret"] ?? throw AppException.Internal(ErrorCodes.TokenProcessFailed, ControlledErrorHelper.TokenProcessFailed);
                var issuer = Configuration["Jwt:Issuer"] ?? throw AppException.Internal(ErrorCodes.TokenProcessFailed, ControlledErrorHelper.TokenProcessFailed);
                var audience = Configuration["Jwt:Audience"] ?? throw AppException.Internal(ErrorCodes.TokenProcessFailed, ControlledErrorHelper.TokenProcessFailed);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw AppException.Unauthorized(ErrorCodes.TokenInvalid, ControlledErrorHelper.InvalidToken);
                }

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    throw AppException.Unauthorized(ErrorCodes.InvalidUserId, "توکن معتبر نیست - شناسه کاربر یافت نشد");
                }

                var user = await UserRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    throw AppException.NotFound(ErrorCodes.NotFound, ControlledErrorHelper.NotFound);
                }

                if (!user.IsActive)
                {
                    throw AppException.Forbidden(ErrorCodes.Forbidden, ControlledErrorHelper.InactiveUserAccount);
                }

                var userDto = new UserResponseDto
                {
                    Id = user.Id,
                    PhoneNumber = user.PhoneNumber,
                    FullName = user.FullName,
                    NationalId = user.NationalId,
                    Email = user.Email,
                    IsActive = user.IsActive,
                    IsPhoneVerified = user.IsPhoneVerified,
                    IsDeleted = user.IsDeleted,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    LastLoginAt = user.LastLoginAt
                };

                return StatusCode(200, ApiResponse<UserResponseDto>.CreateSuccess(userDto));
            }
            catch (SecurityTokenExpiredException)
            {
                throw AppException.Unauthorized(ErrorCodes.TokenExpired, "توکن منقضی شده است");
            }
            catch (SecurityTokenException)
            {
                throw AppException.Unauthorized(ErrorCodes.TokenInvalid, ControlledErrorHelper.InvalidToken);
            }
        }
    }
}



