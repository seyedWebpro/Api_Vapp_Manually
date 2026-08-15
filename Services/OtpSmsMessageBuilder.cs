using System.Text;
using System.Text.RegularExpressions;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ساخت متن استاندارد پیامک OTP برای autofill سیستم‌عامل:
    /// - iOS / Safari: خط <c>@domain #code</c> (Origin-bound one-time code)
    /// - Android SMS Retriever: هش ۱۱ کاراکتری اپ در آخرین خط
    /// - ایران‌نوین: پسوند لغو11 قبل از هش اندروید
    /// </summary>
    public static class OtpSmsMessageBuilder
    {
        private static readonly Regex NonAsciiDigitRegex = new(@"[^\d]", RegexOptions.Compiled);

        /// <summary>
        /// بدنه پیامک بدون پسوند لغو و بدون هش اندروید.
        /// </summary>
        public static string BuildBody(
            string otpCode,
            string templateType = "VerifyOtp",
            string? autofillDomain = null)
        {
            var code = NormalizeOtpDigits(otpCode);
            var headline = templateType switch
            {
                "ResetPassword" => $"کد بازیابی رمز عبور: {code}",
                "ForgotPassword" => $"کد بازیابی رمز عبور: {code}",
                "Register" => $"کد تایید ثبت نام: {code}",
                "Registration" => $"کد تایید ثبت نام: {code}",
                _ => $"کد تایید شما: {code}"
            };

            var domain = NormalizeDomain(autofillDomain);
            if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(code))
                return headline;

            // فرمت استاندارد Apple / Chrome برای پیشنهاد خودکار کد
            return $"{headline}\n\n@{domain} #{code}";
        }

        /// <summary>
        /// متن نهایی آماده ارسال: بدنه + لغو11 + (اختیاری) Android App Hash در آخرین خط.
        /// </summary>
        public static string BuildForSend(
            string otpCode,
            string templateType = "VerifyOtp",
            string? autofillDomain = null,
            string? androidAppHash = null,
            string optOutSuffix = "لغو11")
        {
            var body = BuildBody(otpCode, templateType, autofillDomain);
            var withOptOut = EnsureOptOutSuffix(body, optOutSuffix);
            return AppendAndroidAppHash(withOptOut, androidAppHash);
        }

        /// <summary>
        /// افزودن هش اپ اندروید در آخرین خط (الزام SMS Retriever API).
        /// </summary>
        public static string AppendAndroidAppHash(string message, string? androidAppHash)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            var hash = NormalizeAndroidAppHash(androidAppHash);
            if (string.IsNullOrEmpty(hash))
                return message.TrimEnd();

            var trimmed = message.TrimEnd();
            if (trimmed.EndsWith(hash, StringComparison.Ordinal))
                return trimmed;

            return $"{trimmed}\n{hash}";
        }

        public static string EnsureOptOutSuffix(string message, string optOutSuffix = "لغو11")
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            var suffix = (optOutSuffix ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(suffix))
                return message.TrimEnd();

            if (HasOptOutSuffix(message, suffix))
                return message.TrimEnd();

            return $"{message.TrimEnd()}\n{suffix}";
        }

        /// <summary>
        /// آیا پیامک از قبل «لغو11» دارد (در انتها، یا دقیقاً قبل از خط هش اندروید)؟
        /// </summary>
        public static bool HasOptOutSuffix(string message, string optOutSuffix = "لغو11")
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var suffix = (optOutSuffix ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(suffix))
                return false;

            var trimmed = message.TrimEnd();
            if (trimmed.EndsWith(suffix, StringComparison.Ordinal))
                return true;

            var lines = trimmed.Split('\n');
            if (lines.Length < 2)
                return false;

            var last = lines[^1].Trim();
            var secondLast = lines[^2].Trim();
            return secondLast.EndsWith(suffix, StringComparison.Ordinal)
                   && IsLikelyAndroidAppHash(last);
        }

        public static string NormalizeOtpDigits(string? otpCode)
        {
            if (string.IsNullOrWhiteSpace(otpCode))
                return string.Empty;

            var sb = new StringBuilder(otpCode.Length);
            foreach (var ch in otpCode.Trim())
            {
                sb.Append(ch switch
                {
                    >= '۰' and <= '۹' => (char)('0' + (ch - '۰')),
                    >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
                    _ => ch
                });
            }

            return NonAsciiDigitRegex.Replace(sb.ToString(), string.Empty);
        }

        public static string? NormalizeDomain(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            var value = domain.Trim();
            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                value = value[8..];
            else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                value = value[7..];

            value = value.Trim().TrimStart('@').TrimEnd('/');
            var slash = value.IndexOf('/');
            if (slash >= 0)
                value = value[..slash];

            return string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
        }

        public static string? NormalizeAndroidAppHash(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return null;

            var value = hash.Trim();
            // هش SMS Retriever معمولاً ۱۱ کاراکتر Base64-like است
            if (value.Length is < 8 or > 16)
                return null;

            foreach (var ch in value)
            {
                if (!(char.IsLetterOrDigit(ch) || ch is '+' or '/' or '=' or '-' or '_'))
                    return null;
            }

            return value;
        }

        public static bool IsLikelyAndroidAppHash(string? line)
        {
            var hash = NormalizeAndroidAppHash(line);
            return !string.IsNullOrEmpty(hash);
        }
    }
}
