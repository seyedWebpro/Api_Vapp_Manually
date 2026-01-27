using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای ایجاد کاربر جدید
    /// </summary>
    public class CreateUserDto
    {
        [Required(ErrorMessage = "شماره تلفن الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن صحیح نیست")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [MinLength(6, ErrorMessage = "رمز عبور باید حداقل 6 کاراکتر باشد")]
        public string Password { get; set; } = string.Empty;

        public string? FullName { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "فرمت کد ملی صحیح نیست")]
        public string? NationalId { get; set; }

        [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsPhoneVerified { get; set; } = false;
    }
}

