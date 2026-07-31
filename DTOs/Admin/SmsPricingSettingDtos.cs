using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class SmsPricingSettingResponseDto
    {
        public int Id { get; set; }
        public bool IsBillingEnabled { get; set; }
        public bool IsBillingEffectivelyEnabled { get; set; }
        public bool ServerWalletCheckDisabled { get; set; }
        public decimal CostPerPart { get; set; }

        public int PersianFirstPageChars { get; set; }
        public int PersianSecondPageChars { get; set; }
        public int PersianOtherPagesChars { get; set; }
        public int EnglishFirstPageChars { get; set; }
        public int EnglishOtherPagesChars { get; set; }
        public int MaxPages { get; set; }

        public int RegularCharWeight { get; set; }
        public int SpaceCharWeight { get; set; }
        public int EmojiCharWeight { get; set; }

        public bool TrimContentBeforeCount { get; set; }
        public bool CountLeadingTrailingSpaces { get; set; }
        public int LanguageDetectionSampleLength { get; set; }
        public bool DefaultLanguageIsPersian { get; set; }

        public bool IncludeOptOutSuffixInCalculation { get; set; }
        public string OptOutSuffix { get; set; } = "لغو11";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateSmsPricingSettingDto
    {
        [Required(ErrorMessage = "وضعیت فعال بودن صورتحساب الزامی است")]
        public bool IsBillingEnabled { get; set; }

        [Required(ErrorMessage = "هزینه هر پارت الزامی است")]
        [Range(0, 1000000, ErrorMessage = "هزینه هر پارت باید بین ۰ تا ۱٬۰۰۰٬۰۰۰ تومان باشد")]
        public decimal CostPerPart { get; set; }

        [Range(1, 1000, ErrorMessage = "ظرفیت صفحه اول فارسی باید بین ۱ تا ۱۰۰۰ باشد")]
        public int PersianFirstPageChars { get; set; }

        [Range(1, 1000, ErrorMessage = "ظرفیت صفحه دوم فارسی باید بین ۱ تا ۱۰۰۰ باشد")]
        public int PersianSecondPageChars { get; set; }

        [Range(1, 1000, ErrorMessage = "ظرفیت صفحات بعدی فارسی باید بین ۱ تا ۱۰۰۰ باشد")]
        public int PersianOtherPagesChars { get; set; }

        [Range(1, 2000, ErrorMessage = "ظرفیت صفحه اول انگلیسی باید بین ۱ تا ۲۰۰۰ باشد")]
        public int EnglishFirstPageChars { get; set; }

        [Range(1, 2000, ErrorMessage = "ظرفیت صفحات بعدی انگلیسی باید بین ۱ تا ۲۰۰۰ باشد")]
        public int EnglishOtherPagesChars { get; set; }

        [Range(1, 50, ErrorMessage = "حداکثر صفحات باید بین ۱ تا ۵۰ باشد")]
        public int MaxPages { get; set; }

        [Range(0, 20, ErrorMessage = "وزن کاراکتر معمولی باید بین ۰ تا ۲۰ باشد")]
        public int RegularCharWeight { get; set; }

        [Range(0, 20, ErrorMessage = "وزن فاصله باید بین ۰ تا ۲۰ باشد")]
        public int SpaceCharWeight { get; set; }

        [Range(0, 20, ErrorMessage = "وزن ایموجی باید بین ۰ تا ۲۰ باشد")]
        public int EmojiCharWeight { get; set; }

        public bool TrimContentBeforeCount { get; set; }
        public bool CountLeadingTrailingSpaces { get; set; }

        [Range(1, 500, ErrorMessage = "طول نمونه تشخیص زبان باید بین ۱ تا ۵۰۰ باشد")]
        public int LanguageDetectionSampleLength { get; set; }

        public bool DefaultLanguageIsPersian { get; set; }
        public bool IncludeOptOutSuffixInCalculation { get; set; }

        [Required(ErrorMessage = "پسوند لغو الزامی است")]
        [MaxLength(50, ErrorMessage = "پسوند لغو نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string OptOutSuffix { get; set; } = "لغو11";
    }

    /// <summary>
    /// پیش‌نمایش هزینه بر اساس تنظیمات ذخیره‌شده یا پیش‌نویس فرم (بدون ذخیره).
    /// </summary>
    public class SmsPricingPreviewRequestDto
    {
        [Required(ErrorMessage = "متن پیام الزامی است")]
        [MaxLength(5000, ErrorMessage = "متن پیام نمی‌تواند بیشتر از ۵۰۰۰ کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;

        [Range(1, 1000000, ErrorMessage = "تعداد گیرنده باید بین ۱ تا ۱٬۰۰۰٬۰۰۰ باشد")]
        public int RecipientsCount { get; set; } = 1;

        /// <summary>اگر null باشد از تنظیمات/پیش‌نویس استفاده می‌شود</summary>
        public bool? IncludeOptOutSuffix { get; set; }

        /// <summary>پیش‌نویس تنظیمات برای preview قبل از ذخیره (اختیاری)</summary>
        public UpdateSmsPricingSettingDto? DraftSettings { get; set; }
    }

    public class SmsPricingPreviewResponseDto
    {
        public string Language { get; set; } = "Persian";
        public bool IsPersian { get; set; }
        public int WeightedCharacterCount { get; set; }
        public int RawTextElementCount { get; set; }
        public int SpaceElementCount { get; set; }
        public int EmojiElementCount { get; set; }
        public int RegularElementCount { get; set; }
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
    }
}
