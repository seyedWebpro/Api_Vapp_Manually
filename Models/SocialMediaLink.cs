namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل لینک شبکه‌های اجتماعی
    /// برای مدیریت لینک‌های سوشیال مدیا (اینستاگرام، تلگرام، روبیکا، سروش و ...)
    /// </summary>
    public class SocialMediaLink
    {
        // شناسه یکتای لینک
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // نوع پلتفرم (Instagram, Telegram, WhatsApp, Rubika, Soroush, Eitaa, Bale, ...)
        public string Platform { get; set; } = string.Empty;

        // آدرس لینک (URL)
        public string LinkUrl { get; set; } = string.Empty;

        // آیا پیش‌فرض است
        public bool IsDefault { get; set; } = false;

        // فعال/غیرفعال
        public bool IsActive { get; set; } = true;

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر مالک
        public virtual User User { get; set; } = null!;

        #endregion
    }
}





