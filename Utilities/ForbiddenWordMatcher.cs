using System.Globalization;
using System.Text;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// تطبیق کلمات فیلتر با نرمال‌سازی فارسی/عربی و مرز کلمه — بدون وابستگی به EF.
    /// </summary>
    public static class ForbiddenWordMatcher
    {
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var form = input.Trim().Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(form.Length);

            foreach (var ch in form)
            {
                var mapped = ch switch
                {
                    'ي' or 'ى' or 'ۍ' or 'ێ' => 'ی',
                    'ك' or 'ڪ' => 'ک',
                    'ة' or 'ۀ' => 'ه',
                    'أ' or 'إ' or 'آ' => 'ا',
                    'ؤ' => 'و',
                    'ئ' => 'ی',
                    '\u200c' or '\u200f' or '\u200e' or '\uFEFF' => (char?)null, // ZWNJ / BOM
                    _ => ch
                };

                if (mapped is null)
                    continue;

                var c = mapped.Value;
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                        sb.Append(' ');
                    continue;
                }

                sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// کلمات فیلترشده‌ای که به‌صورت کلمه/عبارت کامل در متن ظاهر شده‌اند.
        /// </summary>
        public static List<string> FindMatches(string? text, IReadOnlyList<(string Display, string Normalized)> forbiddenWords)
        {
            var normalizedText = Normalize(text);
            if (normalizedText.Length == 0 || forbiddenWords.Count == 0)
                return new List<string>();

            var matches = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // عبارات طولانی‌تر اول — جلوگیری از تطبیق ناقص تکراری
            foreach (var (display, normalized) in forbiddenWords.OrderByDescending(w => w.Normalized.Length))
            {
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (!ContainsAsWholePhrase(normalizedText, normalized))
                    continue;

                if (seen.Add(normalized))
                    matches.Add(display);
            }

            return matches;
        }

        public static List<string> FindMatchesInTexts(
            IEnumerable<string?> texts,
            IReadOnlyList<(string Display, string Normalized)> forbiddenWords)
        {
            var all = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var text in texts)
            {
                foreach (var match in FindMatches(text, forbiddenWords))
                {
                    var key = Normalize(match);
                    if (seen.Add(key))
                        all.Add(match);
                }
            }

            return all;
        }

        public static bool ContainsAsWholePhrase(string haystack, string needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
                return false;

            var index = 0;
            while (index <= haystack.Length - needle.Length)
            {
                var found = haystack.IndexOf(needle, index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                var startOk = found == 0 || !IsWordChar(haystack[found - 1]);
                var end = found + needle.Length;
                var endOk = end >= haystack.Length || !IsWordChar(haystack[end]);

                if (startOk && endOk)
                    return true;

                index = found + 1;
            }

            return false;
        }

        private static bool IsWordChar(char c) =>
            char.IsLetterOrDigit(c) || c is '_' or '-' or '\u200c';
    }
}
