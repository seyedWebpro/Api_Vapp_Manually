namespace Api_Vapp.Models
{
    /// <summary>
    /// تنظیمات سراسری سیستم معرفی (رفرال) برای شارژ کیف پول.
    /// معمولاً یک ردیف فعال در دیتابیس وجود دارد که از پنل ادمین مدیریت می‌شود.
    /// </summary>
    public class WalletReferralSetting
    {
        public int Id { get; set; }

        /// <summary>فعال/غیرفعال بودن کل سیستم معرفی</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>درصد تخفیف پرداخت‌کننده (مثلاً ۱۰)</summary>
        public decimal DiscountPercent { get; set; } = 10m;

        /// <summary>درصد پاداش صاحب کد (مثلاً ۱۰)</summary>
        public decimal BonusPercent { get; set; } = 10m;

        /// <summary>
        /// متن توضیحات قابل نمایش در اپ.
        /// جای‌نگهدارنده‌ها: {DiscountPercent} و {BonusPercent}
        /// </summary>
        public string DescriptionTemplate { get; set; } =
            "کافیه کاربر معرفی‌شده این کد رو موقع شارژ کیف پول وارد کنه؛ در این صورت {DiscountPercent}٪ تخفیف براشون اعمال می‌شه و {BonusPercent}٪ پاداش هم به شما واریز می‌شه.";

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
