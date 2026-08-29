namespace Api_Vapp.Constants
{
    /// <summary>
    /// دسته‌بندی وضعیت دلیوری برای نمایش به کاربر
    /// </summary>
    public static class SmsDeliveryCategories
    {
        public const string DeliveredToPhone = "DeliveredToPhone";
        public const string SentToOperator = "SentToOperator";
        public const string NotDelivered = "NotDelivered";
        public const string PendingApproval = "PendingApproval";
        public const string Rejected = "Rejected";
        public const string PendingSync = "PendingSync";
        public const string SendFailed = "SendFailed";

        /// <summary>
        /// دسته‌های نهایی که پیام به گوشی نرسیده و هزینه باید به کیف پول برگردد
        /// </summary>
        public static readonly HashSet<string> WalletRefundEligibleCategories =
        [
            NotDelivered,
            Rejected,
            SendFailed
        ];

        public static readonly IReadOnlyDictionary<string, string> PersianLabels = new Dictionary<string, string>
        {
            [DeliveredToPhone] = "رسیده به گوشی",
            [SentToOperator] = "ارسال به اپراتور",
            [NotDelivered] = "نرسیده به گوشی",
            [PendingApproval] = "منتظر تایید",
            [Rejected] = "رد پیام",
            [PendingSync] = "در انتظار بررسی",
            [SendFailed] = "ارسال ناموفق"
        };

        public static string GetPersianLabel(string category) =>
            PersianLabels.TryGetValue(category, out var label) ? label : category;

        public static bool IsWalletRefundEligible(string category) =>
            !string.IsNullOrWhiteSpace(category) && WalletRefundEligibleCategories.Contains(category);
    }
}
