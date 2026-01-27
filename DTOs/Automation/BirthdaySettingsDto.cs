using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای تنظیمات پایه تبریک تولد
    /// </summary>
    public class BirthdaySettingsDto
    {
        [Required(ErrorMessage = "ساعت ارسال الزامی است")]
        [RegularExpression(@"^([0-1][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "فرمت ساعت باید HH:mm باشد (مثال: 10:00)")]
        public string SendTime { get; set; } = "10:00"; // HH:mm

        [Required(ErrorMessage = "تکرار سالانه الزامی است")]
        public bool RepeatYearly { get; set; } = true;
    }
}

