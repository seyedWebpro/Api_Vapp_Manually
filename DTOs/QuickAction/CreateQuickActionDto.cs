using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.QuickAction
{
    /// <summary>
    /// DTO برای ایجاد اقدام سریع (لینک) جدید
    /// </summary>
    public class CreateQuickActionDto
    {
        [Required(ErrorMessage = "نام لینک الزامی است")]
        [MaxLength(100, ErrorMessage = "نام لینک نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ActionType { get; set; }
        // مقادیر معتبر: InstagramLink, WhatsApp, Location, Telegram, BusinessCard, Custom, PaymentLink

        public string? Content { get; set; }

        // فایل آیکون برای آپلود (اختیاری)
        public IFormFile? IconFile { get; set; }
    }
}












