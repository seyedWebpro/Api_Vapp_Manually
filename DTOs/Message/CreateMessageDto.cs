using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ایجاد پیام جدید
    /// </summary>
    public class CreateMessageDto
    {
        // متن پیام (اختیاری - می‌تواند بعداً به‌روزرسانی شود)
        public string? Content { get; set; }

        /// <summary>
        /// شناسه قالب استفاده‌شده (اختیاری). اگر قالب تأییدشده باشد، ارسال بدون صف تأیید پیام انجام می‌شود.
        /// </summary>
        public int? TemplateId { get; set; }
    }
}


