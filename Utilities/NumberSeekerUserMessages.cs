namespace Api_Vapp.Utilities
{
    /// <summary>
    /// پیام‌های امن شماره‌جو برای موبایل — بدون جزئیات فنی.
    /// خطاهای اشتباه کاربر → راهنمایی اصلاح (بدون ارجاع الکی به پشتیبانی).
    /// خطاهای سیستم/استخراج → پیام ثابت + تلاش مجدد + پشتیبانی در صورت تکرار.
    /// </summary>
    public static class NumberSeekerUserMessages
    {
        public const string ExtractionFailed =
            "مشکل در استخراج شماره‌ها پیش آمده. لطفاً مجدد تلاش کنید و در صورت تکرار با پشتیبانی تماس بگیرید.";

        public const string RateLimited =
            "تعداد درخواست‌های شما زیاد است. لطفاً کمی صبر کنید و دوباره تلاش کنید.";

        public const string InvalidInput =
            "اطلاعات وارد شده ناقص یا نامعتبر است. لطفاً پلتفرم، شهر، دسته و تعداد را بررسی کنید.";

        public const string TaskIdRequired = "شناسه جستجو الزامی است.";

        public const string TaskNotFound = "این جستجو پیدا نشد یا متعلق به شما نیست.";

        public const string AlreadyImported =
            "این جستجو قبلاً در دفترچه ذخیره شده است. برای ذخیره مجدد، گزینه تکرار را فعال کنید.";

        public const string NotReadyForImport =
            "جستجو هنوز تمام نشده است. لطفاً تا پایان صبر کنید.";

        public const string NoPhonesForAction =
            "شماره‌ای برای این عملیات وجود ندارد. لطفاً جستجوی جدیدی انجام دهید یا شهر و دسته را تغییر دهید.";

        public const string ServiceDisabled =
            "در حال حاضر امکان جستجوی شماره فعال نیست. لطفاً بعداً تلاش کنید.";

        public const string Cancelled = "جستجو توسط شما لغو شد.";

        public const string CancelNotAllowed =
            "این جستجو دیگر در حال اجرا نیست و قابل لغو نیست.";

        public const string NoListings =
            "با این شهر و دسته موردی پیدا نشد. لطفاً شهر یا دسته را تغییر دهید و دوباره تلاش کنید.";

        public const string NoPhonesFound =
            "شماره‌ای یافت نشد. لطفاً شهر یا دسته را تغییر دهید و دوباره تلاش کنید.";

        /// <summary>پیام وضعیت تسک برای UI — هرگز متن خام اسکرپر/Exception.</summary>
        public static string ForTaskStatus(string? status, string? resultCode, int phoneCount)
        {
            var st = (status ?? string.Empty).Trim().ToLowerInvariant();
            var code = (resultCode ?? string.Empty).Trim().ToLowerInvariant();

            if (st is "pending" or "running")
                return st == "pending"
                    ? "درخواست شما در صف است."
                    : "در حال استخراج شماره‌ها...";

            if (st == "cancelled" || code == "cancelled")
                return Cancelled;

            if (st is "completed" or "partial" || code is "success" or "partial" or "db_unavailable" or "database_error")
            {
                if (phoneCount > 0)
                    return $"{phoneCount} شماره دریافت شد.";
                if (st is "completed" or "partial")
                    return "جستجو انجام شد.";
            }

            if (code == "no_listings")
                return NoListings;

            if (code is "no_phones" or "all_duplicates")
                return NoPhonesFound;

            if (st == "failed" || !string.IsNullOrEmpty(code))
                return ExtractionFailed;

            return ExtractionFailed;
        }

        public static string SanitizeIncomingUserMessage(string? raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            if (!ControlledErrorHelper.IsSafeUserMessage(raw))
                return fallback;

            var lower = raw.ToLowerInvariant();
            if (lower.Contains("sql") || lower.Contains("odbc") || lower.Contains("traceback") ||
                lower.Contains("http") || lower.Contains("api key") || lower.Contains("selenium") ||
                lower.Contains("chrome") || lower.Contains("driver") || lower.Contains("stack") ||
                lower.Contains("exception") || lower.Contains("db_") || lower.Contains("token") ||
                raw.Contains("localhost") || raw.Contains("127.0.0.1") || raw.Contains(":8000") ||
                raw.Contains(":8080") || raw.Contains("X-API"))
            {
                return fallback;
            }

            return raw.Trim();
        }
    }
}
