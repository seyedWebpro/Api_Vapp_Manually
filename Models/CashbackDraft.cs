using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل Draft برای ذخیره موقت اطلاعات مرحله 1 و 2 کش‌بک
    /// مشابه MessageSession برای پیام‌ها
    /// </summary>
    public class CashbackDraft
    {
        // شناسه یکتای Draft
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // شناسه Draft (یکتا - برای استفاده در API)
        public string DraftId { get; set; } = string.Empty;

        // اطلاعات مرحله 1 (JSON)
        public string Step1Data { get; set; } = string.Empty;

        // اطلاعات مرحله 2 (JSON) - می‌تواند null باشد
        public string? Step2Data { get; set; }

        // اطلاعات مرحله 3 (JSON) - تنظیمات تگ و زمان ارسال - می‌تواند null باشد
        public string? Step3Data { get; set; }

        // تاریخ انقضا (برای پاک‌سازی خودکار - پیش‌فرض: 24 ساعت)
        public DateTime ExpiresAt { get; set; }

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر ایجادکننده
        public virtual User User { get; set; } = null!;

        #endregion
    }
}
