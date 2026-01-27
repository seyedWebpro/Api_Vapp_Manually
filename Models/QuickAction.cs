namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل اقدام سریع
    /// برای گزینه‌های فوری پس از وارد کردن شماره
    /// </summary>
    public class QuickAction
    {
        // شناسه یکتای اقدام سریع
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // نام اقدام (مثلاً "ارسال کارت ویزیت")
        public string Name { get; set; } = string.Empty;

        // نوع اقدام (اختیاری)
        public string? ActionType { get; set; }
        // BusinessCard, InstagramLink, Location, Telegram, WhatsApp, Custom

        // آیکون اقدام
        public string? Icon { get; set; }

        // محتوای اقدام (لینک، متن و غیره)
        public string? Content { get; set; }

        // ترتیب نمایش
        public int DisplayOrder { get; set; } = 0;

        // فعال/غیرفعال
        public bool IsActive { get; set; } = true;

        // آیا پیش‌فرض است
        public bool IsDefault { get; set; } = false;

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


