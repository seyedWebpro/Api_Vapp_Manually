namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل گروه قالب
    /// گروه‌بندی قالب‌های پیام برای سازماندهی بهتر
    /// </summary>
    public class TemplateGroup
    {
        // شناسه یکتای گروه
        public int Id { get; set; }

        // شناسه کاربر ایجادکننده
        public int UserId { get; set; }

        // نام گروه
        public string Name { get; set; } = string.Empty;

        // توضیحات گروه
        public string? Description { get; set; }

        // آیکون گروه
        public string? Icon { get; set; }

        // ترتیب نمایش
        public int DisplayOrder { get; set; } = 0;

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

        // قالب‌های این گروه
        public virtual ICollection<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();

        #endregion
    }
}

