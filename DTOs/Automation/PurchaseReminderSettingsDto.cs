using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای تنظیمات پایه یادآوری خرید
    /// </summary>
    public class PurchaseReminderSettingsDto
    {
        [Required(ErrorMessage = "تعداد روز بدون خرید الزامی است")]
        [Range(1, 365, ErrorMessage = "تعداد روز باید بین 1 تا 365 باشد")]
        public int DaysWithoutPurchase { get; set; }
    }
}

