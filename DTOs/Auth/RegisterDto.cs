using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "نام کامل الزامی است")]
        [StringLength(100, ErrorMessage = "نام کامل نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست (باید با 09 شروع شود و 11 رقم باشد)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد ملی الزامی است")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی باید 10 رقم باشد")]
        public string NationalId { get; set; } = string.Empty;
    }
}



