namespace Api_Vapp.Models
{
    public class Role
    {
        // شناسه یکتای نقش
        public int Id { get; set; }

        // نام نقش (مثلاً: Admin, User, Manager)
        public string Name { get; set; } = string.Empty;

        // نام نمایشی نقش (مثلاً: مدیر سیستم، کاربر عادی، مدیر)
        public string DisplayName { get; set; } = string.Empty;

        // توضیحات نقش
        public string? Description { get; set; }

        // فعال بودن نقش
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

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        #endregion
    }
}

