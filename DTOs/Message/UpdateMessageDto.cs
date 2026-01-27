using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای به‌روزرسانی پیام
    /// همه فیلدها اختیاری هستند - اگر فیلدی null یا خالی باشد، تغییری اعمال نمی‌شود
    /// </summary>
    public class UpdateMessageDto
    {
        public string? Content { get; set; }
    }
}



