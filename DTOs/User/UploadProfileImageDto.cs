using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای آپلود عکس پروفایل
    /// </summary>
    public class UploadProfileImageDto
    {
        [Required(ErrorMessage = "فایل عکس الزامی است")]
        public IFormFile ImageFile { get; set; } = null!;
    }
}



