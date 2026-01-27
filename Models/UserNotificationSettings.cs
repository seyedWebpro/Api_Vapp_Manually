namespace Api_Vapp.Models
{
    /// <summary>
    /// تنظیمات اعلان‌های کاربر
    /// </summary>
    public class UserNotificationSettings
    {
        // شناسه یکتای تنظیمات
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        #region اعلان‌های سیستمی

        // اعلان‌های مهم (تغییرات مهم حساب)
        public bool ImportantNotifications { get; set; } = true;

        // به‌روزرسانی‌ها (نسخه جدید اپ)
        public bool Updates { get; set; } = false;

        // هشدارهای سیستمی (خطا یا اختلال)
        public bool SystemWarnings { get; set; } = true;

        #endregion

        #region اعلان‌های مالی

        // تراکنش کیف پول (واریز یا برداشت)
        public bool WalletTransaction { get; set; } = false;

        // کش بک مشتری (مصرف یا اضافه شدن)
        public bool CustomerCashback { get; set; } = true;

        // گزارش مالی (خلاصه روزانه)
        public bool FinancialReport { get; set; } = false;

        #endregion

        #region اعلان‌های متفرقه

        // ثبت مشتری جدید
        public bool NewCustomerRegistration { get; set; } = false;

        // پیشنهادها (پیشنهاد و کمپین)
        public bool Suggestions { get; set; } = true;

        // آموزش و نکته (راهنمای استفاده)
        public bool EducationAndTips { get; set; } = false;

        #endregion

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        public virtual User User { get; set; } = null!;

        #endregion
    }
}



