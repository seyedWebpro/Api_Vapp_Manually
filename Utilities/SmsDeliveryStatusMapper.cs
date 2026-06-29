using Api_Vapp.Constants;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نگاشت کدهای دلیوری ایران‌نوین (مطابق مستند REST) به دسته‌های گزارش
    /// </summary>
    public static class SmsDeliveryStatusMapper
    {
        /// <summary>
        /// کدهایی که وضعیت نهایی دارند و دیگر نیازی به polling نیست
        /// </summary>
        private static readonly HashSet<int> FinalStatusCodes =
        [
            2, 3, 4, 7, 12, 17, 19, 21, 23, 25, 27, 28
        ];

        /// <summary>
        /// نگاشت کد عددی API به یکی از ۵ دسته گزارش + PendingSync
        /// </summary>
        public static string MapToCategory(int providerStatusCode) => providerStatusCode switch
        {
            // رسیده به گوشی
            2 => SmsDeliveryCategories.DeliveredToPhone,

            // ارسال به اپراتور / در مسیر
            0 or 1 or 5 or 8 or 9 or 10 or 11 or 13 or 14 or 18 or 26 => SmsDeliveryCategories.SentToOperator,

            // نرسیده به گوشی
            3 or 12 or 21 or 28 => SmsDeliveryCategories.NotDelivered,

            // منتظر تایید
            22 => SmsDeliveryCategories.PendingApproval,

            // رد پیام / خطا / بلاک
            4 or 7 or 17 or 19 or 23 or 25 or 27 => SmsDeliveryCategories.Rejected,

            // وضعیت نامشخص یا مشکل بروزرسانی — هنوز نهایی نیست
            6 or 20 => SmsDeliveryCategories.SentToOperator,

            _ => SmsDeliveryCategories.SentToOperator
        };

        public static bool IsFinalStatus(int providerStatusCode) => FinalStatusCodes.Contains(providerStatusCode);

        /// <summary>
        /// نرمال‌سازی شماره برای تطبیق با پاسخ Delivery (API بدون 0 اول برمی‌گرداند: 9110000000)
        /// </summary>
        public static string NormalizeMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return string.Empty;

            var normalized = new string(mobile.Where(char.IsDigit).ToArray());

            if (normalized.StartsWith("98") && normalized.Length >= 12)
                normalized = normalized[2..];
            else if (normalized.StartsWith("0") && normalized.Length == 11)
                normalized = normalized[1..];

            return normalized;
        }

        public static bool MobileMatches(string storedMobile, string providerMobile)
        {
            var a = NormalizeMobile(storedMobile);
            var b = NormalizeMobile(providerMobile);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            return a == b;
        }
    }
}
