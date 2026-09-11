using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// جایگذاری placeholderهای قالب مناسبتی
    /// </summary>
    public static class OccasionMessagePersonalizer
    {
        public static string Apply(
            string template,
            string? contactName,
            string? businessName,
            string? occasionName)
        {
            var result = template ?? string.Empty;
            var fullName = string.IsNullOrWhiteSpace(contactName) ? "مشتری" : contactName.Trim();
            var brand = businessName?.Trim() ?? string.Empty;
            var occasion = occasionName?.Trim() ?? string.Empty;

            result = Replace(result, "{{نام}}", fullName);
            result = Replace(result, "{{name}}", fullName, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{نام}", fullName);

            result = Replace(result, "{{نام برند}}", brand);
            result = Replace(result, "{{brand name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{{brandname}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{نام برند}", brand);

            result = Replace(result, "{{نام شرکت}}", brand);
            result = Replace(result, "{{نام بیزنس}}", brand);
            result = Replace(result, "{{نام کسب و کار}}", brand);
            result = Replace(result, "{{business name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{{company name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{نام شرکت}", brand);
            result = Replace(result, "{نام بیزنس}", brand);

            result = Replace(result, "{{مناسبت}}", occasion);
            result = Replace(result, "{{occasion}}", occasion, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{مناسبت}", occasion);

            return result;
        }

        /// <summary>
        /// برای صف تأیید: نام مخاطب را نگه می‌داریم تا در ConfirmAndSend شخصی‌سازی شود؛
        /// فقط نام کسب‌وکار و مناسبت از قبل جایگزین می‌شود.
        /// </summary>
        public static string ApplyForQueuePreview(string template, string? businessName, string? occasionName)
        {
            var result = template ?? string.Empty;
            var brand = businessName?.Trim() ?? string.Empty;
            var occasion = occasionName?.Trim() ?? string.Empty;

            result = Replace(result, "{{نام برند}}", brand);
            result = Replace(result, "{{brand name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{{brandname}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{نام برند}", brand);
            result = Replace(result, "{{نام شرکت}}", brand);
            result = Replace(result, "{{نام بیزنس}}", brand);
            result = Replace(result, "{{نام کسب و کار}}", brand);
            result = Replace(result, "{{business name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{{company name}}", brand, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{نام شرکت}", brand);
            result = Replace(result, "{نام بیزنس}", brand);
            result = Replace(result, "{{مناسبت}}", occasion);
            result = Replace(result, "{{occasion}}", occasion, StringComparison.OrdinalIgnoreCase);
            result = Replace(result, "{مناسبت}", occasion);

            return result;
        }

        public static string ResolveEffectiveTemplate(
            SpecialOccasion occasion,
            UserOccasionPreference? preference)
        {
            if (preference != null
                && !string.IsNullOrWhiteSpace(preference.CustomMessage)
                && string.Equals(preference.TemplateApprovalStatus, AdminApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            {
                return preference.CustomMessage!;
            }

            return occasion.DefaultMessage ?? string.Empty;
        }

        private static string Replace(string text, string placeholder, string value, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(placeholder))
                return text;

            if (comparison == StringComparison.Ordinal)
                return text.Replace(placeholder, value);

            var escaped = System.Text.RegularExpressions.Regex.Escape(placeholder);
            return System.Text.RegularExpressions.Regex.Replace(text, escaped, value ?? string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
