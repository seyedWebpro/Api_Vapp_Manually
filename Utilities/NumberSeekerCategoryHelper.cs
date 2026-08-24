namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی و اعتبارسنجی دسته‌بندی شماره‌جو.
    /// دسته = کلمهٔ جستجو (آزاد)؛ لیست KnownCategories فقط پیشنهاد UI است.
    /// </summary>
    public static class NumberSeekerCategoryHelper
    {
        public const int MinLength = 1;
        public const int MaxLength = 200;

        public const string Placeholder = "مثال : کافه - رستوران و ...";

        public const string CustomAllowedHint =
            "می‌توانید از پیشنهادها انتخاب کنید یا دستهٔ دلخواه را بنویسید.";

        /// <summary>
        /// Trim + فشرده‌سازی فاصله‌ها. در صورت نامعتبر بودن، error فارسی برمی‌گرداند.
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
            normalized = string.Join(' ', parts);

            if (normalized.Length < MinLength)
            {
                error = "دسته‌بندی الزامی است";
                return false;
            }

            if (normalized.Length > MaxLength)
            {
                error = "دسته‌بندی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد";
                return false;
            }

            var hasLetterOrDigit = false;
            foreach (var ch in normalized)
            {
                if (char.IsControl(ch))
                {
                    error = "دسته‌بندی نامعتبر است";
                    return false;
                }

                if (char.IsLetterOrDigit(ch))
                    hasLetterOrDigit = true;
            }

            if (!hasLetterOrDigit)
            {
                error = "دسته‌بندی نامعتبر است";
                return false;
            }

            return true;
        }
    }
}
