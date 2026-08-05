using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// درخواست ارسال OTP برای تغییر شماره موبایل پروفایل
    /// </summary>
    public class RequestChangePhoneDto
    {
        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// تایید OTP و اعمال تغییر شماره موبایل
    /// </summary>
    public class VerifyChangePhoneDto
    {
        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد تایید الزامی است")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "کد تایید باید 4 رقم باشد")]
        public string OtpCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// پاسخ ارسال/ارسال مجدد OTP تغییر شماره
    /// </summary>
    public class ChangePhoneOtpResponseDto
    {
        public int ExpiresInSeconds { get; set; }
        public int? RetryAfterSeconds { get; set; }

        // DEV ONLY — TODO(production): قبل از release این property را حذف کنید (جستجو: OtpCode)
        public string? OtpCode { get; set; }
    }

    /// <summary>
    /// داده کش OTP تغییر شماره — شامل userId برای جلوگیری از سوءاستفاده
    /// </summary>
    public class ChangePhoneOtpCacheDto
    {
        public string OtpCode { get; set; } = string.Empty;
        public string NewPhoneNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
    }
}
