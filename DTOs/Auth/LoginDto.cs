using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تماس صحیح نیست (باید با 09 شروع شود و 11 رقم باشد)")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}



