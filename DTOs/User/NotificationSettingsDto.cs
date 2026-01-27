namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای تنظیمات اعلان‌های کاربر
    /// </summary>
    public class NotificationSettingsDto
    {
        #region اعلان‌های سیستمی

        /// <summary>
        /// اعلان‌های مهم (تغییرات مهم حساب)
        /// </summary>
        public bool ImportantNotifications { get; set; }

        /// <summary>
        /// به‌روزرسانی‌ها (نسخه جدید اپ)
        /// </summary>
        public bool Updates { get; set; }

        /// <summary>
        /// هشدارهای سیستمی (خطا یا اختلال)
        /// </summary>
        public bool SystemWarnings { get; set; }

        #endregion

        #region اعلان‌های مالی

        /// <summary>
        /// تراکنش کیف پول (واریز یا برداشت)
        /// </summary>
        public bool WalletTransaction { get; set; }

        /// <summary>
        /// کش بک مشتری (مصرف یا اضافه شدن)
        /// </summary>
        public bool CustomerCashback { get; set; }

        /// <summary>
        /// گزارش مالی (خلاصه روزانه)
        /// </summary>
        public bool FinancialReport { get; set; }

        #endregion

        #region اعلان‌های متفرقه

        /// <summary>
        /// ثبت مشتری جدید
        /// </summary>
        public bool NewCustomerRegistration { get; set; }

        /// <summary>
        /// پیشنهادها (پیشنهاد و کمپین)
        /// </summary>
        public bool Suggestions { get; set; }

        /// <summary>
        /// آموزش و نکته (راهنمای استفاده)
        /// </summary>
        public bool EducationAndTips { get; set; }

        #endregion
    }
}



