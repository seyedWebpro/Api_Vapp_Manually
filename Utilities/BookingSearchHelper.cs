using System.Text;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی عبارت جستجوی مشتری رزرو (فاصله و حروف عربی/فارسی).
    /// </summary>
    public static class BookingSearchHelper
    {
        public static string NormalizeTerm(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return string.Empty;
            }

            var trimmed = term.Trim();
            var sb = new StringBuilder(trimmed.Length);
            var previousWasSpace = false;

            foreach (var ch in trimmed)
            {
                var normalized = ch switch
                {
                    'ي' => 'ی',
                    'ك' => 'ک',
                    '\u200c' => ' ', // ZWNJ
                    _ => ch
                };

                if (char.IsWhiteSpace(normalized))
                {
                    if (previousWasSpace)
                    {
                        continue;
                    }

                    sb.Append(' ');
                    previousWasSpace = true;
                    continue;
                }

                previousWasSpace = false;
                sb.Append(normalized);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// نسخه‌های جایگزین برای تطبیق نام ذخیره‌شده با ی/ك عربی.
        /// </summary>
        public static IReadOnlyList<string> BuildNameVariants(string normalizedTerm)
        {
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                return Array.Empty<string>();
            }

            var arabicYeKaf = normalizedTerm
                .Replace('ی', 'ي')
                .Replace('ک', 'ك');

            if (arabicYeKaf == normalizedTerm)
            {
                return new[] { normalizedTerm };
            }

            return new[] { normalizedTerm, arabicYeKaf };
        }
    }
}
