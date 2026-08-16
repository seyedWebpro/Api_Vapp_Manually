using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Public
{
    public class RegisterPublicParticipantDto
    {
        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام خانوادگی الزامی است")]
        [MaxLength(100, ErrorMessage = "نام خانوادگی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [MaxLength(20, ErrorMessage = "شماره موبایل نامعتبر است")]
        public string ParticipantMobile { get; set; } = string.Empty;
    }

    public class RegisterPublicParticipantResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        public bool IsPhoneVerified { get; set; }

        /// <summary>
        /// برای فرم: زمان انقضای OTP (ثانیه). برای گردونه معمولاً null است.
        /// </summary>
        public int? OtpExpiresInSeconds { get; set; }

        /// <summary>
        /// زمان انتظار تا امکان ارسال مجدد OTP
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        // DEV ONLY — قبل از انتشار production حذف شود
        public string? OtpCode { get; set; }
    }

    public class VerifyPublicParticipantOtpDto
    {
        [Required(ErrorMessage = "توکن دسترسی الزامی است")]
        [MaxLength(200, ErrorMessage = "توکن دسترسی نامعتبر است")]
        public string AccessToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد تایید الزامی است")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "کد تایید باید ۴ رقم باشد")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ResendPublicParticipantOtpDto
    {
        [Required(ErrorMessage = "توکن دسترسی الزامی است")]
        [MaxLength(200, ErrorMessage = "توکن دسترسی نامعتبر است")]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class PublicParticipantOtpResponseDto
    {
        public int ExpiresInSeconds { get; set; }

        public int? RetryAfterSeconds { get; set; }

        public bool IsPhoneVerified { get; set; }

        /// <summary>
        /// انقضای جلسه دسترسی (بعد از تأیید OTP تمدید می‌شود)
        /// </summary>
        public DateTime? SessionExpiresAt { get; set; }

        // DEV ONLY — قبل از انتشار production حذف شود
        public string? OtpCode { get; set; }
    }
}
