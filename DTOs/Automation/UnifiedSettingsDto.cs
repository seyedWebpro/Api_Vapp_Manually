using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO یکپارچه برای تنظیمات همه انواع پیام خودکار
    /// </summary>
    public class UnifiedSettingsDto
    {
        /// <summary>
        /// نوع تنظیمات: Birthday, CashbackExpiry, Welcome, PurchaseReminder, SpecialOccasion, Custom
        /// </summary>
        [Required(ErrorMessage = "نوع تنظیمات الزامی است")]
        [RegularExpression("^(?i)(Birthday|CashbackExpiry|Welcome|PurchaseReminder|SpecialOccasion|Custom)$",
            ErrorMessage = "نوع تنظیمات باید یکی از موارد زیر باشد: Birthday, CashbackExpiry, Welcome, PurchaseReminder, SpecialOccasion, Custom")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// داده‌های تنظیمات تبریک تولد
        /// </summary>
        public BirthdaySettingsData? BirthdaySettings { get; set; }

        /// <summary>
        /// داده‌های تنظیمات یادآوری انقضای کش‌بک
        /// </summary>
        public CashbackExpirySettingsData? CashbackExpirySettings { get; set; }

        /// <summary>
        /// داده‌های تنظیمات یادآوری خرید
        /// </summary>
        public PurchaseReminderSettingsData? PurchaseReminderSettings { get; set; }

        /// <summary>
        /// داده‌های تنظیمات مناسبت‌های خاص
        /// </summary>
        public SpecialOccasionSettingsData? SpecialOccasionSettings { get; set; }

        /// <summary>
        /// داده‌های تنظیمات اتوماسیون سفارشی
        /// </summary>
        public CustomAutomationSettingsData? CustomAutomationSettings { get; set; }
    }

    /// <summary>
    /// داده‌های تنظیمات تبریک تولد
    /// </summary>
    public class BirthdaySettingsData
    {
        [Required(ErrorMessage = "ساعت ارسال الزامی است")]
        [RegularExpression(@"^([0-1][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "فرمت ساعت باید HH:mm باشد (مثال: 10:00)")]
        public string SendTime { get; set; } = "10:00";

        [Required(ErrorMessage = "تکرار سالانه الزامی است")]
        public bool RepeatYearly { get; set; } = true;
    }

    /// <summary>
    /// داده‌های تنظیمات یادآوری انقضای کش‌بک
    /// </summary>
    public class CashbackExpirySettingsData
    {
        [Required(ErrorMessage = "تعداد روز قبل از انقضا الزامی است")]
        [Range(1, 365, ErrorMessage = "تعداد روز باید بین 1 تا 365 باشد")]
        public int DaysBeforeExpiry { get; set; }

        [Required(ErrorMessage = "حالت اجرا الزامی است")]
        [RegularExpression("^(Once|Multiple)$", ErrorMessage = "حالت اجرا باید Once یا Multiple باشد")]
        public string ExecutionMode { get; set; } = "Once";
    }

    /// <summary>
    /// داده‌های تنظیمات یادآوری خرید
    /// </summary>
    public class PurchaseReminderSettingsData
    {
        [Required(ErrorMessage = "تعداد روز بدون خرید الزامی است")]
        [Range(1, 365, ErrorMessage = "تعداد روز باید بین 1 تا 365 باشد")]
        public int DaysWithoutPurchase { get; set; }
    }

    /// <summary>
    /// داده‌های تنظیمات مناسبت‌های خاص
    /// </summary>
    public class SpecialOccasionSettingsData
    {
        [Required(ErrorMessage = "نوع عملیات الزامی است")]
        [RegularExpression("^(Add|Remove)$", ErrorMessage = "نوع عملیات باید Add یا Remove باشد")]
        public string Action { get; set; } = string.Empty;

        // برای افزودن مناسبت
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? OccasionName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? OccasionDate { get; set; }

        // برای حذف مناسبت
        public int? OccasionId { get; set; }
    }

    /// <summary>
    /// داده‌های تنظیمات اتوماسیون سفارشی
    /// </summary>
    public class CustomAutomationSettingsData
    {
        [Required(ErrorMessage = "شرایط فعال‌سازی الزامی است")]
        public string ActivationConditions { get; set; } = string.Empty;
    }
}

