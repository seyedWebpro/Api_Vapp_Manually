using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل Session برای ذخیره موقت انتخاب گیرندگان در پیام عادی
    /// این مدل برای پیام‌های عادی استفاده می‌شود و کمپین ایجاد نمی‌کند
    /// </summary>
    public class MessageSession
    {
        // شناسه یکتای Session
        public int Id { get; set; }

        // شناسه پیام مربوطه (الزامی - پیام باید قبل از انتخاب گیرندگان ایجاد شده باشد)
        public int MessageId { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // معیارهای انتخاب گیرندگان (JSON)
        // شامل: SelectionType, ContactNotebookIds, TagIds, ContactIds, MobileNumbers, FullNames
        public string SelectionCriteria { get; set; } = string.Empty;

        // لیست گیرندگان نهایی (JSON) - برای جلوگیری از محاسبه مجدد
        // شامل: List<RecipientItemDto>
        public string? RecipientsJson { get; set; }

        // آیا این Session استفاده شده است (برای ارسال)
        public bool IsUsed { get; set; } = false;

        // تاریخ انقضا (برای پاک‌سازی خودکار - پیش‌فرض: 24 ساعت)
        public DateTime? ExpiresAt { get; set; }

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        // RowVersion برای Optimistic Concurrency Control
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // پیام مربوطه
        public virtual Message Message { get; set; } = null!;

        // کاربر ایجادکننده
        public virtual User User { get; set; } = null!;

        #endregion
    }
}

