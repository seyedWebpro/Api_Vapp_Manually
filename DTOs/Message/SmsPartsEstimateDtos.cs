using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// درخواست تخمین دقیق کاراکتر/صفحه پیامک برای کلاینت (موبایل/پابلیک) —
    /// همان موتور محاسبه پنل ادمین، بدون امکان پیش‌نویس تنظیمات.
    /// </summary>
    public class SmsPartsEstimateRequestDto
    {
        /// <summary>متن پیام (می‌تواند خالی باشد برای شمارنده زنده)</summary>
        [MaxLength(5000, ErrorMessage = "متن پیام نمی‌تواند بیشتر از ۵۰۰۰ کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;

        [Range(1, 1000000, ErrorMessage = "تعداد گیرنده باید بین ۱ تا ۱٬۰۰۰٬۰۰۰ باشد")]
        public int RecipientsCount { get; set; } = 1;
    }

    /// <summary>
    /// پاسخ تخمین کاراکتر وزن‌دار و تعداد صفحه/پارت — هم‌تراز با پیش‌نمایش ادمین.
    /// </summary>
    public class SmsPartsEstimateResponseDto
    {
        public string Language { get; set; } = "Persian";
        public bool IsPersian { get; set; }

        /// <summary>شمارش کاراکتر وزن‌دار (منبع حقیقت برای UI — نه String.length)</summary>
        public int WeightedCharacterCount { get; set; }

        public int RawTextElementCount { get; set; }
        public int SpaceElementCount { get; set; }
        public int EmojiElementCount { get; set; }
        public int RegularElementCount { get; set; }

        /// <summary>تعداد صفحه/پارت پیامک</summary>
        public int PartsCount { get; set; }

        public int MaxPages { get; set; }
        public bool ExceedsMaxPages { get; set; }
        public bool OptOutApplied { get; set; }
        public string PreparedContentPreview { get; set; } = string.Empty;

        public decimal CostPerPart { get; set; }
        public int RecipientsCount { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public bool IsBillingEffectivelyEnabled { get; set; }
        public string BillingNote { get; set; } = string.Empty;

        /// <summary>ظرفیت صفحات فعلی (برای نمایش پیشرفت در UI در صورت نیاز)</summary>
        public int PersianFirstPageChars { get; set; }
        public int PersianSecondPageChars { get; set; }
        public int PersianOtherPagesChars { get; set; }
        public int EnglishFirstPageChars { get; set; }
        public int EnglishOtherPagesChars { get; set; }

        public int RegularCharWeight { get; set; }
        public int SpaceCharWeight { get; set; }
        public int EmojiCharWeight { get; set; }
        public string OptOutSuffix { get; set; } = "لغو11";
    }
}
