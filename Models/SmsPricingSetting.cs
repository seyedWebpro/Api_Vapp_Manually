namespace Api_Vapp.Models
{
    /// <summary>
    /// تنظیمات سراسری تعرفه و قواعد محاسبه پارت پیامک.
    /// معمولاً یک ردیف فعال در دیتابیس وجود دارد که از پنل ادمین مدیریت می‌شود.
    /// </summary>
    public class SmsPricingSetting
    {
        public int Id { get; set; }

        /// <summary>فعال بودن کسر هزینه از کیف پول کاربر</summary>
        public bool IsBillingEnabled { get; set; }

        /// <summary>هزینه هر پارت پیامک (تومان)</summary>
        public decimal CostPerPart { get; set; } = 160m;

        // —— ظرفیت صفحات فارسی ——
        public int PersianFirstPageChars { get; set; } = 70;
        public int PersianSecondPageChars { get; set; } = 64;
        public int PersianOtherPagesChars { get; set; } = 67;

        // —— ظرفیت صفحات انگلیسی ——
        public int EnglishFirstPageChars { get; set; } = 160;
        public int EnglishOtherPagesChars { get; set; } = 153;

        /// <summary>حداکثر تعداد صفحات مجاز برای یک پیام</summary>
        public int MaxPages { get; set; } = 10;

        // —— وزن شمارش کاراکتر ——
        /// <summary>وزن حروف/اعداد/علائم معمولی</summary>
        public int RegularCharWeight { get; set; } = 1;

        /// <summary>وزن فاصله‌ها و whitespace (فاصله، تب، خط جدید و ...)</summary>
        public int SpaceCharWeight { get; set; } = 1;

        /// <summary>وزن هر ایموجی (text element)</summary>
        public int EmojiCharWeight { get; set; } = 3;

        /// <summary>قبل از شمارش، Trim روی ابتدا/انتهای متن اعمال شود</summary>
        public bool TrimContentBeforeCount { get; set; } = true;

        /// <summary>
        /// اگر Trim خاموش باشد: آیا فاصله‌های ابتدا/انتهای متن هم شمرده شوند؟
        /// (وقتی Trim روشن است این فیلد بی‌اثر است چون فاصله‌های لبه حذف می‌شوند)
        /// </summary>
        public bool CountLeadingTrailingSpaces { get; set; } = true;

        /// <summary>طول نمونه ابتدای متن برای تشخیص زبان</summary>
        public int LanguageDetectionSampleLength { get; set; } = 50;

        /// <summary>اگر زبان تشخیص داده نشود، پیش‌فرض فارسی باشد</summary>
        public bool DefaultLanguageIsPersian { get; set; } = true;

        /// <summary>در محاسبه و ارسال، پسوند لغو اجباری همیشه لحاظ می‌شود (الزام سرویس پیامکی).</summary>
        public bool IncludeOptOutSuffixInCalculation { get; set; } = true;

        /// <summary>متن پسوند لغو (بدون خط جدید؛ خط جدید جداگانه اضافه می‌شود)</summary>
        public string OptOutSuffix { get; set; } = "لغو11";

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
