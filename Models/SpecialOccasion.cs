namespace Api_Vapp.Models
{
    /// <summary>
    /// مناسبت‌های سالانه (سیستمی یا سفارشی کاربر)
    /// اعیاد، عزاداری‌ها، و مناسبت‌های خاص کسب‌وکار (مثل سالروز تاسیس)
    /// </summary>
    public class SpecialOccasion
    {
        public int Id { get; set; }

        /// <summary>null برای مناسبت‌های سیستمی</summary>
        public int? UserId { get; set; }

        /// <summary>کد یکتای سیستمی (مثلاً NOROOZ) — برای کاربر null</summary>
        public string? Code { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Holiday | Death | Custom</summary>
        public string Type { get; set; } = "Custom";

        /// <summary>Congratulation | Condolence</summary>
        public string Category { get; set; } = "Congratulation";

        /// <summary>Jalali | Gregorian | Hijri</summary>
        public string CalendarType { get; set; } = "Jalali";

        /// <summary>ماه در تقویم مشخص‌شده (۱–۱۲)</summary>
        public byte Month { get; set; }

        /// <summary>روز در تقویم مشخص‌شده</summary>
        public byte Day { get; set; }

        /// <summary>
        /// تاریخ مرجع سازگاری با نسخه‌های قبلی (date-only UTC).
        /// برای تطبیق سالانه از Month/Day/CalendarType استفاده می‌شود.
        /// </summary>
        public DateTime OccasionDate { get; set; }

        /// <summary>قالب پیش‌فرض ادمین (می‌تواند شامل {{نام}}، {{نام برند}}، {{نام شرکت}}، {{مناسبت}} باشد)</summary>
        public string? DefaultMessage { get; set; }

        public int SortOrder { get; set; }

        public bool IsSystem { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User? User { get; set; }

        public virtual ICollection<AutomatedMessage> AutomatedMessages { get; set; } = new List<AutomatedMessage>();

        public virtual ICollection<UserOccasionPreference> UserPreferences { get; set; } = new List<UserOccasionPreference>();
    }
}
