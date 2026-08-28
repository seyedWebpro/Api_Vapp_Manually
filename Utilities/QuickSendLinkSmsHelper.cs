namespace Api_Vapp.Utilities
{
    /// <summary>
    /// ساخت متن SMS ارسال سریع برای آیتم‌های لینک‌دار
    /// (کارت ویزیت / رزرو / فرم / گردونه / لینک سوشیال).
    /// </summary>
    public static class QuickSendLinkSmsHelper
    {
        public const int MaxCaptionLength = 100;

        /// <summary>
        /// نرمال‌سازی توضیحات ارسال — خالی → null؛ فاصله/خط‌جدید داخلی → یک فاصله؛
        /// طول بیش از MaxCaptionLength → برش (دفاعی؛ اعتبارسنجی اصلی در DTO است).
        /// </summary>
        public static string? NormalizeCaption(string? value)
        {
            if (value == null)
                return null;

            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            var normalized = string.Join(' ', parts);
            if (normalized.Length == 0)
                return null;

            if (normalized.Length > MaxCaptionLength)
                normalized = normalized[..MaxCaptionLength].TrimEnd();

            return normalized.Length == 0 ? null : normalized;
        }

        /// <summary>
        /// متن نهایی SMS: در صورت وجود توضیحات → «توضیحات + خط جدید + URL»، وگرنه فقط URL.
        /// </summary>
        public static string BuildSmsContent(string? caption, string publicUrl)
        {
            var url = (publicUrl ?? string.Empty).Trim();
            var normalizedCaption = NormalizeCaption(caption);

            if (string.IsNullOrEmpty(normalizedCaption))
                return url;

            if (string.IsNullOrEmpty(url))
                return normalizedCaption;

            return $"{normalizedCaption}\n{url}";
        }
    }
}
