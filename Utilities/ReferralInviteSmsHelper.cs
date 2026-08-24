using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// قالب پیامک دعوت/پاداش معرف — بدنه سیستمی ثابت است و فقط جمله پایانی دعوت قابل ویرایش است.
    /// </summary>
    public static class ReferralInviteSmsHelper
    {
        public const int ClosingTextMaxLength = 200;

        public const string DefaultClosingText =
            "کد را به دوستانتان بدهید تا با استفاده از آن پاداش فعال شود.";

        public const string DefaultReferrerRewardClosingText =
            "برای دریافت پاداش مراجعه کنید.";

        public const string SamplePersonalCode = "REF123456";

        public static string NormalizeClosingText(string? text)
        {
            var trimmed = (text ?? string.Empty).Trim();
            return string.IsNullOrEmpty(trimmed) ? DefaultClosingText : trimmed;
        }

        public static bool TryValidateClosingText(string? text, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (text.Trim().Length > ClosingTextMaxLength)
            {
                error = $"متن پایانی پیامک حداکثر {ClosingTextMaxLength} کاراکتر است";
                return false;
            }

            return true;
        }

        public static bool IsCustomClosingText(string? text)
        {
            return !string.Equals(
                NormalizeClosingText(text),
                DefaultClosingText,
                StringComparison.Ordinal);
        }

        public static string BuildInviteMessage(
            string programTitle,
            string personalCode,
            string rewardType,
            bool isCustomerRewardActive,
            decimal? customerRewardValue,
            bool isReferrerRewardActive,
            decimal referrerRewardValue,
            string? closingText)
        {
            var parts = new List<string>
            {
                $"برنامه پاداش «{programTitle}»",
                $"کد معرف شما: {personalCode}"
            };

            if (isCustomerRewardActive && customerRewardValue.HasValue)
            {
                parts.Add($"تخفیف مشتری: {FormatRewardValue(rewardType, customerRewardValue.Value)}");
            }

            if (isReferrerRewardActive && referrerRewardValue > 0)
            {
                parts.Add($"پاداش معرف: {FormatRewardValue(rewardType, referrerRewardValue)}");
            }

            parts.Add(NormalizeClosingText(closingText));
            return string.Join("\n", parts);
        }

        public static string BuildReferrerRewardMessage(
            string programTitle,
            decimal rewardAmount)
        {
            return
                $"شما {rewardAmount:N0} تومان پاداش گرفتید چون کسی با کد معرف شما خرید کرده است.\n" +
                $"برنامه: «{programTitle}»\n" +
                DefaultReferrerRewardClosingText;
        }

        public static string FormatRewardValue(string rewardType, decimal value)
        {
            return rewardType == ReferralRewardTypes.Percentage
                ? $"{value:N0}%"
                : $"{value:N0} تومان";
        }
    }
}
