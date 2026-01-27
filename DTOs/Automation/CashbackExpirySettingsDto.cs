using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای تنظیمات پایه یادآوری انقضای کش‌بک
    /// </summary>
    public class CashbackExpirySettingsDto
    {
        [Required(ErrorMessage = "تعداد روز قبل از انقضا الزامی است")]
        [Range(1, 365, ErrorMessage = "تعداد روز باید بین 1 تا 365 باشد")]
        public int DaysBeforeExpiry { get; set; }

        [Required(ErrorMessage = "حالت اجرا الزامی است")]
        [RegularExpression("^(Once|Multiple)$", ErrorMessage = "حالت اجرا باید Once یا Multiple باشد")]
        public string ExecutionMode { get; set; } = "Once"; // Once, Multiple
    }
}

