using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای آپلود فایل‌های ضمیمه
    /// </summary>
    public class UploadAttachmentsDto
    {
        [Required(ErrorMessage = "حداقل یک فایل الزامی است")]
        public List<IFormFile> Files { get; set; } = new List<IFormFile>();
    }
}

