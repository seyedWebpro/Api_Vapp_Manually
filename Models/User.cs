namespace Api_Vapp.Models
{
    public class User
    {
        // شناسه یکتای کاربر
        public int Id { get; set; }

        // شماره تلفن (برای ورود و احراز هویت)
        public string PhoneNumber { get; set; } = string.Empty;

        // هش رمز عبور
        public string PasswordHash { get; set; } = string.Empty;

        // نام کامل
        public string? FullName { get; set; }

        // کد ملی
        public string? NationalId { get; set; }

        // ایمیل
        public string? Email { get; set; }

        // فعال بودن حساب کاربری
        public bool IsActive { get; set; } = true;

        // تأیید شده بودن شماره تلفن
        public bool IsPhoneVerified { get; set; } = false;

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        // موجودی کیف پول
        public decimal WalletBalance { get; set; } = 0;

        // مسیر عکس پروفایل
        public string? ProfileImagePath { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        // تاریخ و زمان آخرین ورود
        public DateTime? LastLoginAt { get; set; }

        #endregion

        #region Navigation Properties

        // نقش‌های کاربر
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        #endregion
    }
}
