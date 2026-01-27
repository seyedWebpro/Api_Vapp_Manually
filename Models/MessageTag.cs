namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل تگ پیام
    /// برای دسته‌بندی و فیلتر کردن مخاطبین
    /// </summary>
    public class MessageTag
    {
        // شناسه یکتای تگ
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // نام تگ
        public string Name { get; set; } = string.Empty;

        // رنگ تگ (hex)
        public string? Color { get; set; }

        // توضیحات
        public string? Description { get; set; }

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

        // مخاطبین دارای این تگ
        public virtual ICollection<ContactTag> ContactTags { get; set; } = new List<ContactTag>();

        #endregion
    }
}


