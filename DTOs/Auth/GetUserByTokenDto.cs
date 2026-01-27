using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Auth
{
    /// <summary>
    /// DTO برای دریافت اطلاعات کاربر از توکن
    /// </summary>
    public class GetUserByTokenDto
    {
        [Required(ErrorMessage = "توکن الزامی است")]
        public string Token { get; set; } = string.Empty;
    }
}

