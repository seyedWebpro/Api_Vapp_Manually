namespace Api_Vapp.Models
{
    /// <summary>
    /// پروفایل سراسری کاربر برای جدول تبریک/تسلیت مناسبتی
    /// </summary>
    public class UserOccasionProfile
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>نام کسب‌وکار / شرکت برای جایگذاری در قالب SMS</summary>
        public string? BusinessName { get; set; }

        /// <summary>فعال بودن دسته‌ی تبریک‌ها</summary>
        public bool CongratulationsEnabled { get; set; } = true;

        /// <summary>فعال بودن دسته‌ی تسلیت‌ها</summary>
        public bool CondolencesEnabled { get; set; } = true;

        /// <summary>ساعت ارسال روزانه به وقت تهران (مثلاً 10:00)</summary>
        public TimeSpan? ScheduledTimeTehran { get; set; }

        /// <summary>لینک به AutomatedMessage نوع SpecialOccasion (اختیاری)</summary>
        public int? AutomatedMessageId { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual AutomatedMessage? AutomatedMessage { get; set; }
    }
}
