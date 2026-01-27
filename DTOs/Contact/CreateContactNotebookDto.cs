using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای ایجاد دفترچه جدید
    /// </summary>
    public class CreateContactNotebookDto
    {
        [Required(ErrorMessage = "نام دفترچه الزامی است")]
        [StringLength(200, ErrorMessage = "نام دفترچه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 1000 کاراکتر باشد")]
        public string? Description { get; set; }

        // فایل آیکون (اختیاری)
        public IFormFile? Icon { get; set; }

        public bool IsActive { get; set; } = true;
    }
}


