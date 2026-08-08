using System.Text.RegularExpressions;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی و اعتبارسنجی نوع شبکه‌های اجتماعی کارت ویزیت
    /// </summary>
    public static class BusinessCardSocialNetworkHelper
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "instagram",
            "telegram",
            "whatsapp",
            "linkedin",
            "twitter",
            "youtube",
            "facebook",
            "tiktok",
            "snapchat",
            "rubika",
            "soroush",
            "eitaa",
            "bale",
            "website",
            "custom"
        };

        private static readonly Dictionary<string, string> DefaultLabelsFa =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["instagram"] = "اینستاگرام",
                ["telegram"] = "تلگرام",
                ["whatsapp"] = "واتساپ",
                ["linkedin"] = "لینکدین",
                ["twitter"] = "توییتر",
                ["youtube"] = "یوتیوب",
                ["facebook"] = "فیسبوک",
                ["tiktok"] = "تیک‌تاک",
                ["snapchat"] = "اسنپ‌چت",
                ["rubika"] = "روبیکا",
                ["soroush"] = "سروش",
                ["eitaa"] = "ایتا",
                ["bale"] = "بله",
                ["website"] = "وب‌سایت",
                ["custom"] = "لینک"
            };

        public static bool IsAllowed(string? networkType)
        {
            var normalized = NormalizeType(networkType);
            return normalized != null && AllowedTypes.Contains(normalized);
        }

        public static string? NormalizeType(string? networkType)
        {
            if (string.IsNullOrWhiteSpace(networkType))
            {
                return null;
            }

            var trimmed = networkType.Trim().ToLowerInvariant();
            return AllowedTypes.Contains(trimmed) ? trimmed : null;
        }

        public static string ResolveDisplayLabel(string networkType, string? customLabel)
        {
            if (!string.IsNullOrWhiteSpace(customLabel))
            {
                return customLabel.Trim();
            }

            return DefaultLabelsFa.TryGetValue(networkType, out var fa)
                ? fa
                : networkType;
        }

        public static string? NormalizeBankDigits(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = Regex.Replace(value, @"[^\d]", string.Empty);
            if (digits.Length == 0)
            {
                return null;
            }

            return digits.Length > maxLength ? digits[..maxLength] : digits;
        }

        /// <summary>
        /// نرمال‌سازی شبا: فقط ارقام یا با پیشوند IR — خروجی همیشه IR + ۲۴ رقم در صورت کامل بودن
        /// </summary>
        public static (string? Normalized, string? Error) NormalizeSheba(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (null, null);
            }

            var trimmed = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (trimmed.StartsWith("IR", StringComparison.Ordinal))
            {
                trimmed = trimmed[2..];
            }

            var digits = Regex.Replace(trimmed, @"[^\d]", string.Empty);
            if (digits.Length == 0)
            {
                return (null, "شماره شبا نامعتبر است");
            }

            if (digits.Length != 24)
            {
                return (null, "شماره شبا باید ۲۴ رقم باشد");
            }

            return ($"IR{digits}", null);
        }

        public static (string? Normalized, string? Error) NormalizeCardNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (null, null);
            }

            var digits = Regex.Replace(value, @"[^\d]", string.Empty);
            if (digits.Length == 0)
            {
                return (null, "شماره کارت نامعتبر است");
            }

            if (digits.Length != 16)
            {
                return (null, "شماره کارت باید ۱۶ رقم باشد");
            }

            return (digits, null);
        }

        public static (string? Normalized, string? Error) NormalizeAccountNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (null, null);
            }

            var digits = Regex.Replace(value, @"[^\d]", string.Empty);
            if (digits.Length == 0)
            {
                return (null, "شماره حساب نامعتبر است");
            }

            if (digits.Length > BusinessCardConstants.MaxBankAccountLength)
            {
                return (null, $"شماره حساب نمی‌تواند بیشتر از {BusinessCardConstants.MaxBankAccountLength} رقم باشد");
            }

            return (digits, null);
        }
    }
}
