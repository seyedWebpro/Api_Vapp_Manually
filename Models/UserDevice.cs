namespace Api_Vapp.Models
{
    /// <summary>
    /// دستگاه کاربر و توکن FCM برای ارسال Push Notification
    /// </summary>
    public class UserDevice
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>
        /// توکن FCM دریافت‌شده از اپ موبایل
        /// </summary>
        public string FcmToken { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
