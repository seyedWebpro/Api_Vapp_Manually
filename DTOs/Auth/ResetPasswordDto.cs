using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Api_Vapp.DTOs.Auth
{
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد تایید الزامی است")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "کد تایید باید 4 رقم باشد")]
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "رمز عبور باید بین 8 تا 100 کاراکتر باشد")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", 
            ErrorMessage = "رمز عبور باید شامل حروف کوچک، حروف بزرگ و اعداد باشد")]
        public string NewPassword { get; set; } = string.Empty;
    }
}



