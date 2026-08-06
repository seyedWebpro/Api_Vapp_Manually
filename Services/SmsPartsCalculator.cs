using System.Globalization;

namespace Api_Vapp.Services
{
    /// <summary>
    /// محاسبه دقیق تعداد پارت‌های پیامک بر اساس قواعد قابل تنظیم ادمین.
    /// فاصله‌ها، ایموجی و ظرفیت صفحات با وزن‌های تعریف‌شده شمرده می‌شوند.
    /// </summary>
    public static class SmsPartsCalculator
    {
        /// <summary>
        /// محاسبه تعداد پارت‌ها. در صورت عبور از MaxPages استثنا پرتاب می‌شود.
        /// </summary>
        public static int CalculateParts(string content, SmsPartsRules? rules = null)
        {
            rules ??= SmsPartsRules.Defaults;
            var analysis = Analyze(content, rules, throwOnMaxPages: true);
            return analysis.PartsCount;
        }

        /// <summary>
        /// تلاش برای محاسبه پارت بدون پرتاب استثنا.
        /// اگر از MaxPages بیشتر باشد false برمی‌گرداند.
        /// </summary>
        public static bool TryCalculateParts(
            string? content,
            SmsPartsRules? rules,
            out int partsCount,
            out SmsPartsAnalysis analysis)
        {
            rules ??= SmsPartsRules.Defaults;
            analysis = Analyze(content, rules, throwOnMaxPages: false);
            partsCount = analysis.PartsCount;
            return !analysis.ExceedsMaxPages;
        }

        /// <summary>
        /// متن نهایی دقیقاً همان‌طور که هنگام ارسال آماده می‌شود.
        /// پسوند لغو (لغو11) طبق الزام سرویس پیامکی همیشه اعمال می‌شود.
        /// </summary>
        public static string PrepareForSend(string? content, SmsPartsRules? rules = null)
        {
            rules ??= SmsPartsRules.Defaults;
            return PrepareContent(content, rules, applyOptOut: true);
        }

        /// <summary>هزینه = پارت × تعرفه × تعداد گیرنده (گرد شده به ۲ رقم)</summary>
        public static decimal CalculateCost(int partsCount, decimal costPerPart, int recipientsCount = 1)
        {
            if (recipientsCount < 0)
                recipientsCount = 0;

            var safeParts = Math.Max(1, partsCount);
            return Math.Round(costPerPart * safeParts * recipientsCount, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// تخمین زنده پارت/هزینه برای ارسال انبوه بر اساس تعرفه فعلی.
        /// برای پیام شخصی‌سازی‌شده، placeholder با مقادیر نمونهٔ بلند جایگزین می‌شود تا کم‌برآورد نشود.
        /// </summary>
        public static (int PartsCount, decimal TotalCost, bool ExceedsMaxPages) EstimateBulkCost(
            string? content,
            bool isPersonalized,
            int recipientsCount,
            SmsPricingRuntime pricing)
        {
            var estimateContent = isPersonalized
                ? ExpandPlaceholdersForCostEstimate(content ?? string.Empty)
                : (content ?? string.Empty);

            var analysis = Analyze(estimateContent, pricing.Rules, throwOnMaxPages: false);
            var total = CalculateCost(analysis.PartsCount, pricing.CostPerPart, recipientsCount);
            return (analysis.PartsCount, total, analysis.ExceedsMaxPages);
        }

        /// <summary>
        /// جایگزینی محافظه‌کارانهٔ placeholderها برای تخمین سقف هزینه (بدون دسترسی به دیتابیس).
        /// </summary>
        public static string ExpandPlaceholdersForCostEstimate(string template)
        {
            const string sampleName = "نام‌خانوادگی‌بلندنمونه";
            const string sampleAmount = "۱۲۳,۴۵۶,۷۸۹ تومان";
            const string sampleBrand = "نام‌برند‌نمونه‌بلند";
            const string sampleDate = "۱۴۰۴/۱۲/۲۹";

            var result = template;
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\{\{نام\}\}|\{\{name\}\}|\(نام\)|\{نام\}",
                sampleName,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\{\{مبلغ کش بک\}\}|\{\{cashback amount\}\}|\{\{cashbackamount\}\}|\{مبلغ کش بک\}",
                sampleAmount,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\{\{نام برند\}\}|\{\{brand name\}\}|\{\{brandname\}\}|\{نام برند\}",
                sampleBrand,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\{\{تاریخ عضویت\}\}|\{\{membership date\}\}|\{\{membershipdate\}\}|\{تاریخ عضویت\}",
                sampleDate,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\{\{تاریخ خرید\}\}|\{\{purchase date\}\}|\{\{purchasedate\}\}|\{تاریخ خرید\}",
                sampleDate,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\(\(.+?\)\)|\{\{.+?\}\}", sampleName);
            return result;
        }

        /// <summary>شمارش وزن‌دار کاراکترها (فاصله/ایموجی با وزن تنظیم‌شده)</summary>
        public static int CountMessageCharacters(string content, SmsPartsRules? rules = null)
        {
            rules ??= SmsPartsRules.Defaults;
            return Analyze(content, rules, throwOnMaxPages: false).WeightedCharacterCount;
        }

        /// <summary>تشخیص فارسی بودن متن بر اساس نمونه ابتدایی</summary>
        public static bool IsPersian(string content, SmsPartsRules? rules = null)
        {
            rules ??= SmsPartsRules.Defaults;
            var prepared = PrepareContent(content, rules, applyOptOut: false);
            if (string.IsNullOrEmpty(prepared))
                return rules.DefaultLanguageIsPersian;

            return DetectLanguage(prepared, rules);
        }

        /// <summary>
        /// تحلیل کامل برای preview و گزارش: زبان، کاراکترها، پارت، جزئیات فاصله/ایموجی.
        /// </summary>
        public static SmsPartsAnalysis Analyze(
            string? content,
            SmsPartsRules rules,
            bool throwOnMaxPages,
            bool? includeOptOutOverride = null)
        {
            rules ??= SmsPartsRules.Defaults;
            // پسوند لغو طبق الزام سرویس پیامکی همیشه در محاسبه لحاظ می‌شود
            // (includeOptOutOverride فقط برای سناریوهای تستی صریح false می‌تواند باشد)
            var applyOptOut = includeOptOutOverride ?? true;
            var prepared = PrepareContent(content, rules, applyOptOut);

            if (string.IsNullOrEmpty(prepared))
            {
                return new SmsPartsAnalysis
                {
                    PreparedContent = string.Empty,
                    IsPersian = rules.DefaultLanguageIsPersian,
                    WeightedCharacterCount = 0,
                    RawTextElementCount = 0,
                    SpaceElementCount = 0,
                    EmojiElementCount = 0,
                    RegularElementCount = 0,
                    PartsCount = 1,
                    MaxPages = rules.MaxPages,
                    ExceedsMaxPages = false,
                    OptOutApplied = false
                };
            }

            var counts = CountElements(prepared, rules);
            var isPersian = DetectLanguage(prepared, rules);
            var pages = isPersian
                ? CalculatePersianPages(counts.WeightedTotal, rules)
                : CalculateEnglishPages(counts.WeightedTotal, rules);

            var exceeds = pages > rules.MaxPages;
            if (exceeds && throwOnMaxPages)
            {
                throw new ArgumentException(
                    $"تعداد صفحات پیامک ({pages}) از حداکثر مجاز ({rules.MaxPages} صفحه) بیشتر است. لطفاً محتوا را کوتاه کنید.",
                    nameof(content));
            }

            return new SmsPartsAnalysis
            {
                PreparedContent = prepared,
                IsPersian = isPersian,
                WeightedCharacterCount = counts.WeightedTotal,
                RawTextElementCount = counts.RawElements,
                SpaceElementCount = counts.Spaces,
                EmojiElementCount = counts.Emojis,
                RegularElementCount = counts.Regular,
                PartsCount = Math.Max(1, pages),
                MaxPages = rules.MaxPages,
                ExceedsMaxPages = exceeds,
                OptOutApplied = applyOptOut && !string.IsNullOrWhiteSpace(rules.OptOutSuffix)
                    && prepared.Contains(rules.OptOutSuffix.Trim(), StringComparison.Ordinal)
            };
        }

        /// <summary>متن نهایی برای محاسبه (Trim / فاصله لبه / پسوند لغو)</summary>
        public static string PrepareContent(string? content, SmsPartsRules rules, bool applyOptOut)
        {
            content ??= string.Empty;

            string prepared;
            if (rules.TrimContentBeforeCount)
            {
                prepared = content.Trim();
            }
            else if (!rules.CountLeadingTrailingSpaces)
            {
                prepared = content.Trim();
            }
            else
            {
                prepared = content;
            }

            if (!applyOptOut || string.IsNullOrWhiteSpace(rules.OptOutSuffix))
                return prepared;

            var suffix = rules.OptOutSuffix.Trim();
            if (string.IsNullOrEmpty(suffix))
                return prepared;

            if (prepared.TrimEnd().EndsWith(suffix, StringComparison.Ordinal))
                return prepared;

            if (string.IsNullOrEmpty(prepared))
                return suffix;

            return $"{prepared.TrimEnd()}\n{suffix}";
        }

        private static int CalculatePersianPages(int totalChars, SmsPartsRules rules)
        {
            var first = Math.Max(1, rules.PersianFirstPageChars);
            var second = Math.Max(1, rules.PersianSecondPageChars);
            var other = Math.Max(1, rules.PersianOtherPagesChars);

            if (totalChars <= first)
                return 1;

            var remaining = totalChars - first;
            if (remaining <= second)
                return 2;

            remaining -= second;
            return 2 + (int)Math.Ceiling(remaining / (double)other);
        }

        private static int CalculateEnglishPages(int totalChars, SmsPartsRules rules)
        {
            var first = Math.Max(1, rules.EnglishFirstPageChars);
            var other = Math.Max(1, rules.EnglishOtherPagesChars);

            if (totalChars <= first)
                return 1;

            var remaining = totalChars - first;
            return 1 + (int)Math.Ceiling(remaining / (double)other);
        }

        private static bool DetectLanguage(string content, SmsPartsRules rules)
        {
            if (string.IsNullOrWhiteSpace(content))
                return rules.DefaultLanguageIsPersian;

            var sampleLen = Math.Max(1, rules.LanguageDetectionSampleLength);
            var sample = content.Length > sampleLen ? content[..sampleLen] : content;

            var hasPersianChars = false;
            var hasEnglishChars = false;

            foreach (var c in sample)
            {
                if ((c >= 0x0600 && c <= 0x06FF) ||
                    (c >= 0xFB50 && c <= 0xFDFF) ||
                    (c >= 0xFE70 && c <= 0xFEFF))
                {
                    hasPersianChars = true;
                }
                else if ((c >= 0x0020 && c <= 0x007E) || (c >= 0x00A0 && c <= 0x00FF))
                {
                    if (char.IsLetter(c) || char.IsDigit(c))
                        hasEnglishChars = true;
                }
            }

            if (hasPersianChars)
                return true;
            if (hasEnglishChars)
                return false;

            return rules.DefaultLanguageIsPersian;
        }

        private static ElementCounts CountElements(string content, SmsPartsRules rules)
        {
            var regularWeight = Math.Max(0, rules.RegularCharWeight);
            var spaceWeight = Math.Max(0, rules.SpaceCharWeight);
            var emojiWeight = Math.Max(0, rules.EmojiCharWeight);

            var counts = new ElementCounts();
            var textElements = StringInfo.GetTextElementEnumerator(content);

            while (textElements.MoveNext())
            {
                var element = textElements.GetTextElement();
                counts.RawElements++;

                if (IsEmoji(element))
                {
                    counts.Emojis++;
                    counts.WeightedTotal += emojiWeight;
                }
                else if (IsWhitespaceElement(element))
                {
                    counts.Spaces++;
                    counts.WeightedTotal += spaceWeight;
                }
                else
                {
                    counts.Regular++;
                    counts.WeightedTotal += regularWeight;
                }
            }

            return counts;
        }

        private static bool IsWhitespaceElement(string element)
        {
            if (string.IsNullOrEmpty(element))
                return false;

            foreach (var c in element)
            {
                if (!char.IsWhiteSpace(c))
                    return false;
            }

            return true;
        }

        private static bool IsEmoji(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var textElements = StringInfo.GetTextElementEnumerator(text);
            while (textElements.MoveNext())
            {
                var element = textElements.GetTextElement();

                if (element.Length >= 2)
                {
                    var codePoint = char.ConvertToUtf32(element, 0);
                    if ((codePoint >= 0x1F300 && codePoint <= 0x1F9FF) ||
                        (codePoint >= 0x1FA00 && codePoint <= 0x1FAFF) ||
                        (codePoint >= 0x1F1E0 && codePoint <= 0x1F1FF))
                    {
                        return true;
                    }
                }
                else if (element.Length == 1)
                {
                    var codePoint = char.ConvertToUtf32(element, 0);
                    if ((codePoint >= 0x2600 && codePoint <= 0x26FF) ||
                        (codePoint >= 0x2700 && codePoint <= 0x27BF) ||
                        (codePoint >= 0xFE00 && codePoint <= 0xFE0F) ||
                        codePoint == 0x200D ||
                        codePoint == 0x20E3)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private struct ElementCounts
        {
            public int WeightedTotal;
            public int RawElements;
            public int Spaces;
            public int Emojis;
            public int Regular;
        }
    }

    /// <summary>نتیجه تحلیل پارت برای preview و گزارش</summary>
    public sealed class SmsPartsAnalysis
    {
        public string PreparedContent { get; init; } = string.Empty;
        public bool IsPersian { get; init; }
        public int WeightedCharacterCount { get; init; }
        public int RawTextElementCount { get; init; }
        public int SpaceElementCount { get; init; }
        public int EmojiElementCount { get; init; }
        public int RegularElementCount { get; init; }
        public int PartsCount { get; init; }
        public int MaxPages { get; init; }
        public bool ExceedsMaxPages { get; init; }
        public bool OptOutApplied { get; init; }
    }
}
