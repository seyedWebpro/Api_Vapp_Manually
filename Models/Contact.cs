namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل مخاطب (Contact)
    /// هر مخاطب متعلق به یک دفترچه است
    /// </summary>
    public class Contact
    {
        // شناسه یکتای مخاطب
        public int Id { get; set; }

        // شناسه دفترچه
        public int ContactNotebookId { get; set; }

        // شماره موبایل (الزامی)
        public string MobileNumber { get; set; } = string.Empty;

        // نام کامل
        public string? FullName { get; set; }

        // برند (اختیاری)
        public string? Brand { get; set; }

        // برچسب‌ها (به صورت JSON یا جداگانه)
        // برای سادگی، به صورت رشته ذخیره می‌شود و می‌تواند بعداً به جدول جداگانه تبدیل شود
        public string? Tags { get; set; }

        // مسیر عکس پروفایل
        public string? ProfileImagePath { get; set; }

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // دفترچه مربوطه
        public virtual ContactNotebook ContactNotebook { get; set; } = null!;

        // اطلاعات تکمیلی
        public virtual ContactAdditionalInfo? AdditionalInfo { get; set; }

        // مناسبت‌های مخاطب
        public virtual ICollection<ContactOccasion> Occasions { get; set; } = new List<ContactOccasion>();

        // تگ‌های مخاطب
        public virtual ICollection<ContactTag> ContactTags { get; set; } = new List<ContactTag>();

        // گیرنده‌های پیام
        public virtual ICollection<MessageRecipient> MessageRecipients { get; set; } = new List<MessageRecipient>();

        #endregion
    }
}


