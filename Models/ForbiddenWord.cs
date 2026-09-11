namespace Api_Vapp.Models
{
    /// <summary>کلمه یا عبارت فیلترشده که در متون نیازمند تأیید مجاز نیست.</summary>
    public class ForbiddenWord
    {
        public int Id { get; set; }

        /// <summary>کلمه/عبارت اصلی ثبت‌شده توسط ادمین.</summary>
        public string Word { get; set; } = string.Empty;

        /// <summary>نسخه نرمال‌شده برای یکتایی و تطبیق سریع.</summary>
        public string NormalizedWord { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
