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
    }
}


