namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل دفترچه تلفن (Contact Notebook)
    /// هر کاربر می‌تواند چندین دفترچه داشته باشد
    /// </summary>
    public class ContactNotebook
    {
        // شناسه یکتای دفترچه
        public int Id { get; set; }

        // شناسه کاربر مالک دفترچه
        public int UserId { get; set; }

        // نام دفترچه
        public string Name { get; set; } = string.Empty;

        // توضیحات دفترچه
        public string? Description { get; set; }

        // آیکون دفترچه (نام فایل یا URL)
        public string? Icon { get; set; }

        // وضعیت فعال/غیرفعال
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

        // کاربر مالک دفترچه
        public virtual User User { get; set; } = null!;

        // مخاطبین این دفترچه
        public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();

        #endregion
    }
}


