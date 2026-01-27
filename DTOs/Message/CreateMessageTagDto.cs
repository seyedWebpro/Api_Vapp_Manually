using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ایجاد تگ جدید
    /// </summary>
    public class CreateMessageTagDto
    {
        [Required(ErrorMessage = "نام تگ الزامی است")]
        [StringLength(100, ErrorMessage = "نام تگ نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [StringLength(7, ErrorMessage = "رنگ تگ نمی‌تواند بیشتر از 7 کاراکتر باشد")]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "فرمت رنگ صحیح نیست (باید به فرمت hex باشد مثل #FF0000)")]
        public string? Color { get; set; }

        [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }
    }
}

