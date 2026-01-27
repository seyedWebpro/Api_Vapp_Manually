using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای تنظیمات پایه اتوماسیون سفارشی
    /// </summary>
    public class CustomAutomationSettingsDto
    {
        [Required(ErrorMessage = "شرایط فعال‌سازی الزامی است")]
        public string ActivationConditions { get; set; } = string.Empty; // JSON string
    }
}

