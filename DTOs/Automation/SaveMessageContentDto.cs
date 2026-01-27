using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ذخیره محتوای پیام خودکار (مرحله 4 - ساخت پیام)
    /// </summary>
    public class SaveMessageContentDto
    {
        [Required(ErrorMessage = "محتوا الزامی است")]
        [MaxLength(10000, ErrorMessage = "محتوا نمی‌تواند بیشتر از 10000 کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;
    }
}

