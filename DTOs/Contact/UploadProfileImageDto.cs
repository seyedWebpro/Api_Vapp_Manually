using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای آپلود عکس پروفایل
    /// </summary>
    public class UploadProfileImageDto
    {
        [Required(ErrorMessage = "فایل تصویر الزامی است")]
        public IFormFile ImageFile { get; set; } = null!;
    }
}

