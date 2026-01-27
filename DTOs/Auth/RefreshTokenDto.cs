using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Auth
{
    public class RefreshTokenDto
    {
        [Required(ErrorMessage = "Refresh Token الزامی است")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}



