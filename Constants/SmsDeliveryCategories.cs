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
    }
}
