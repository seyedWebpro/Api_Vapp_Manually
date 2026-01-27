using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Auth
{
    public class VerifyOtpDto
    {
        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد تایید الزامی است")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "کد تایید باید 4 رقم باشد")]
        public string OtpCode { get; set; } = string.Empty;
    }
}



