namespace Api_Vapp.Models
{
    public class UserRole
    {
        // شناسه یکتای رابطه کاربر-نقش
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // شناسه نقش
        public int RoleId { get; set; }

        // فعال بودن رابطه
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

        public virtual User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;

        #endregion
    }
}

