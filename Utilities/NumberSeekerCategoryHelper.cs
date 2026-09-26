namespace Api_Vapp.Utilities
{
    /// <summary>
    /// دسته‌بندی شماره‌جو فقط از فهرست ثابت (دراپ‌داون) مجاز است.
    /// پایه: دسته‌های نقشهٔ اینباکسینو + مشاغل پرتکرار تکمیلی (~۷۰).
    /// </summary>
    public static class NumberSeekerCategoryHelper
    {
        public const int MinLength = 1;
        public const int MaxLength = 80;

        public const string Placeholder = "دسته شغلی را انتخاب کنید";

        public const string CustomAllowedHint = "";

        public const string CustomDisabledHint = "فقط از فهرست می‌توانید انتخاب کنید.";

        public const string NotInListError = "دسته‌بندی باید از فهرست انتخاب شود";

        /// <summary>
        /// فهرست canonical برای UI، بانک شماره و اعتبارسنجی API.
        /// </summary>
        public static readonly string[] KnownCategories =
        {
            // خوراک و نوشیدنی
            "رستوران",
            "فست فود",
            "کافه",
            "کترینگ",
            "طباخی",
            "جیگرکی",
            "شیرینی فروشی",
            "نانوایی",
            "آبمیوه و بستنی",
            "آجیل و خشکبار",
            "سوپرمارکت",
            "میوه فروشی",
            "بازار میوه و تره بار",

            // زیبایی و پوشاک
            "آرایشگاه و سالن زیبایی",
            "مزون لباس عروس",
            "بوتیک و لباس فروشی",
            "پوشاک کودک",
            "طلا و جواهر",
            "عینک فروشی",

            // سلامت
            "داروخانه",
            "مطب پزشک",
            "دندانپزشکی",
            "درمانگاه",
            "بیمارستان",
            "آزمایشگاه",
            "مرکز بهداشت",
            "عطاری",
            "فیزیوتراپی",
            "دامپزشکی",
            "لوازم بهداشتی",

            // خودرو
            "تعمیرگاه خودرو",
            "کارواش",
            "تعویض روغن",
            "لوازم یدکی خودرو",
            "نمایشگاه خودرو",
            "امداد خودرو",
            "خدمات زیبایی خودرو",
            "پمپ بنزین",

            // خرید و الکترونیک
            "موبایل فروشی",
            "لوازم الکترونیک",
            "لوازم خانگی",
            "لوازم تحریر و کتاب فروشی",
            "اسباب بازی فروشی",
            "گل فروشی",
            "صنایع دستی",
            "بازار و مرکز خرید",

            // خدمات کسب‌وکار
            "مشاور املاک",
            "دفتر بیمه",
            "وکیل",
            "حسابداری",
            "چاپ و تبلیغات",
            "آتلیه",
            "خشکشویی",
            "کافی نت",
            "امور مشترکین",
            "تاکسی تلفنی",

            // آموزش و کودک
            "آموزشگاه",
            "مهدکودک",
            "پیش دبستانی",
            "خانه بازی",

            // اقامت و تفریح
            "هتل و مهمانپذیر",
            "اقامتگاه بومگردی",
            "آژانس مسافرتی",
            "سینما",
            "تئاتر",
            "شهربازی",
            "موزه",
            "باشگاه ورزشی",
            "پارکینگ",
        };

        private static readonly Dictionary<string, string> CanonicalByKey =
            BuildCanonicalLookup();

        private static Dictionary<string, string> BuildCanonicalLookup()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in KnownCategories)
            {
                var key = ToLookupKey(name);
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                    map[key] = name;
            }

            // مترادف‌های رایج UI → نام canonical
            void Alias(string alias, string canonical)
            {
                var key = ToLookupKey(alias);
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                    map[key] = canonical;
            }

            Alias("فست‌فود", "فست فود");
            Alias("شیرینی‌فروشی", "شیرینی فروشی");
            Alias("کافه رستوران", "کافه");
            Alias("کافی شاپ", "کافه");
            Alias("آرایشگاه", "آرایشگاه و سالن زیبایی");
            Alias("آرایشگاه زنانه", "آرایشگاه و سالن زیبایی");
            Alias("آرایشگاه مردانه", "آرایشگاه و سالن زیبایی");
            Alias("سالن زیبایی", "آرایشگاه و سالن زیبایی");
            Alias("املاک", "مشاور املاک");
            Alias("هتل", "هتل و مهمانپذیر");
            Alias("کلینیک", "درمانگاه");
            Alias("پزشک", "مطب پزشک");
            Alias("موبایل", "موبایل فروشی");
            Alias("پوشاک", "بوتیک و لباس فروشی");
            Alias("خودرو", "نمایشگاه خودرو");

            return map;
        }

        /// <summary>
        /// Trim + یکسان‌سازی فاصله؛ فقط اگر در فهرست ثابت باشد قبول می‌شود و نام canonical برمی‌گردد.
        /// </summary>
        public static bool TryNormalize(string? raw, out string normalized, out string? error)
        {
            normalized = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "دسته‌بندی الزامی است";
                return false;
            }

            var parts = raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var collapsed = string.Join(' ', parts);

            if (collapsed.Length < MinLength)
            {
                error = "دسته‌بندی الزامی است";
                return false;
            }

            if (collapsed.Length > MaxLength)
            {
                error = $"دسته‌بندی نمی‌تواند بیشتر از {ToPersianDigits(MaxLength)} کاراکتر باشد";
                return false;
            }

            foreach (var ch in collapsed)
            {
                if (char.IsControl(ch))
                {
                    error = "دسته‌بندی نامعتبر است";
                    return false;
                }
            }

            var key = ToLookupKey(collapsed);
            if (string.IsNullOrEmpty(key) || !CanonicalByKey.TryGetValue(key, out var canonical))
            {
                error = NotInListError;
                return false;
            }

            normalized = canonical;
            return true;
        }

        public static bool IsKnown(string? raw) =>
            TryNormalize(raw, out _, out _);

        private static string ToLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                if (ch == '\u200c' || char.IsWhiteSpace(ch))
                    continue;

                sb.Append(ch switch
                {
                    'ي' or 'ى' => 'ی',
                    'ك' => 'ک',
                    _ => char.ToLowerInvariant(ch)
                });
            }

            return sb.ToString();
        }

        private static string ToPersianDigits(int value)
        {
            var s = value.ToString();
            return s
                .Replace('0', '۰')
                .Replace('1', '۱')
                .Replace('2', '۲')
                .Replace('3', '۳')
                .Replace('4', '۴')
                .Replace('5', '۵')
                .Replace('6', '۶')
                .Replace('7', '۷')
                .Replace('8', '۸')
                .Replace('9', '۹');
        }
    }
}
