namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل مناسبت‌های خاص
    /// برای پیام‌های خودکار در مناسبت‌های سال (اعیاد، وفات‌ها و غیره)
    /// </summary>
    public class SpecialOccasion
    {
        // شناسه یکتای مناسبت
        public int Id { get; set; }

        // شناسه کاربر (null برای مناسبت‌های سیستمی)
        public int? UserId { get; set; }

        // نام مناسبت
        public string Name { get; set; } = string.Empty;

        // نوع مناسبت (Holiday, Death, Custom)
        public string Type { get; set; } = "Custom"; // Holiday, Death, Custom

        // تاریخ مناسبت (فقط روز و ماه)
        public DateTime OccasionDate { get; set; }

        // متن پیش‌فرض پیام
        public string? DefaultMessage { get; set; }

        // آیا مناسبت سیستمی است
        public bool IsSystem { get; set; } = false;

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

        // کاربر ایجادکننده (در صورت سفارشی بودن)
        public virtual User? User { get; set; }

        // پیام‌های خودکار استفاده‌کننده
        public virtual ICollection<AutomatedMessage> AutomatedMessages { get; set; } = new List<AutomatedMessage>();

        #endregion
    }
}


