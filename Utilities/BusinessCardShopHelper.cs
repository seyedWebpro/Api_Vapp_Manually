namespace Api_Vapp.Utilities
{
    /// <summary>
    /// اعتبارسنجی و متادیتای بخش فروشگاه کارت ویزیت
    /// </summary>
    public static class BusinessCardShopHelper
    {
        /// <summary>متن دکمه فروشگاه در کارت عمومی</summary>
        public const string ButtonLabel = "فروشگاه";

        /// <summary>راهنمای زیر فیلد لینک در ویرایشگر — اگر کاربر فروشگاه آنلاین ندارد</summary>
        public const string NoStoreHint =
            "اگر فروشگاه ندارید برای ایجاد کردن فروشگاه با ما تماس بگیرید، ۰۲۱۵۱۰۹۱۰۰۰";

        /// <summary>شماره تماس پشتیبانی برای ساخت فروشگاه (tel:)</summary>
        public const string ContactPhone = "02151091000";

        private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
        {
            "http",
            "https"
        };

        private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "javascript",
            "data",
            "file",
            "vbscript"
        };

        /// <summary>
        /// نرمال‌سازی URL فروشگاه — در صورت نبود scheme، https:// اضافه می‌شود
        /// </summary>
        public static (string? Normalized, string? Error) NormalizeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (null, null);
            }

            var trimmed = value.Trim();
            if (trimmed.Length > BusinessCardConstants.MaxShopUrlLength)
            {
                return (null, $"آدرس فروشگاه نمی‌تواند بیشتر از {BusinessCardConstants.MaxShopUrlLength} کاراکتر باشد");
            }

            if (trimmed.Contains('@', StringComparison.Ordinal))
            {
                return (null, "آدرس فروشگاه نامعتبر است");
            }

            var withScheme = trimmed;
            if (TryExtractScheme(trimmed, out var explicitScheme, out var remainder))
            {
                if (!AllowedSchemes.Contains(explicitScheme))
                {
                    return (null, "آدرس فروشگاه نامعتبر است");
                }

                withScheme = $"{explicitScheme.ToLowerInvariant()}://{remainder}";
            }
            else
            {
                withScheme = "https://" + trimmed;
            }

            if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) ||
                !AllowedSchemes.Contains(uri.Scheme))
            {
                return (null, "آدرس فروشگاه نامعتبر است");
            }

            if (!IsAllowedHost(uri.Host))
            {
                return (null, "آدرس فروشگاه نامعتبر است");
            }

            return (uri.GetLeftPart(UriPartial.Path) + uri.Query + uri.Fragment, null);
        }

        private static bool TryExtractScheme(string value, out string scheme, out string remainder)
        {
            scheme = string.Empty;
            remainder = value;

            var separatorIndex = value.IndexOf("://", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                return false;
            }

            scheme = value[..separatorIndex];
            remainder = value[(separatorIndex + 3)..];
            return true;
        }

        private static bool IsAllowedHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            var normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
            if (BlockedHosts.Contains(normalizedHost))
            {
                return false;
            }

            if (normalizedHost is "localhost" or "127.0.0.1" or "::1")
            {
                return true;
            }

            if (normalizedHost.Contains(':'))
            {
                normalizedHost = normalizedHost.Split(':')[0];
            }

            return normalizedHost.Contains('.') && !normalizedHost.StartsWith('.') && !normalizedHost.EndsWith('.');
        }
    }
}
