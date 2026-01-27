using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای به‌روزرسانی کاربر
    /// تمام فیلدها اختیاری هستند - اگر null یا empty باشند، مقدار قبلی تغییر نمی‌کند
    /// </summary>
    public class UpdateUserDto
    {
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن صحیح نیست")]
        public string? PhoneNumber { get; set; }

        [MinLength(6, ErrorMessage = "رمز عبور باید حداقل 6 کاراکتر باشد")]
        public string? Password { get; set; }

        public string? FullName { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "فرمت کد ملی صحیح نیست")]
        public string? NationalId { get; set; }

        [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
        public string? Email { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsPhoneVerified { get; set; }
    }
}

