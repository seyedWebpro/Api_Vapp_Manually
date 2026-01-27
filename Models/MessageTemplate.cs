namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل قالب پیام
    /// قالب‌های از پیش تعریف شده برای پیام‌ها
    /// </summary>
    public class MessageTemplate
    {
        // شناسه یکتای قالب
        public int Id { get; set; }

        // شناسه کاربر ایجادکننده
        public int UserId { get; set; }

        // نام قالب
        public string Name { get; set; } = string.Empty;

        // متن قالب
        public string Content { get; set; } = string.Empty;

        // دسته‌بندی قالب
        public string? Category { get; set; }

        // شناسه گروه قالب (اختیاری)
        public int? GroupId { get; set; }

        // توضیحات قالب
        public string? Description { get; set; }

        // آیکون قالب
        public string? Icon { get; set; }

        // آیا قالب پیش‌فرض است
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

        // کاربر ایجادکننده
        public virtual User User { get; set; } = null!;

        // گروه قالب
        public virtual TemplateGroup? Group { get; set; }

        // پیام‌های استفاده کننده از این قالب
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

        #endregion
    }
}


