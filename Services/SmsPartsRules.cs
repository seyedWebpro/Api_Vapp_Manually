namespace Api_Vapp.Services
{
    /// <summary>
    /// قواعد محاسباتی پارت پیامک (اسنپ‌شات غیرقابل تغییر برای مسیرهای داغ ارسال).
    /// </summary>
    public sealed class SmsPartsRules
    {
        public int PersianFirstPageChars { get; init; } = 70;
        public int PersianSecondPageChars { get; init; } = 64;
        public int PersianOtherPagesChars { get; init; } = 67;
        public int EnglishFirstPageChars { get; init; } = 160;
        public int EnglishOtherPagesChars { get; init; } = 153;
        public int MaxPages { get; init; } = 10;
        public int RegularCharWeight { get; init; } = 1;
        public int SpaceCharWeight { get; init; } = 1;
        public int EmojiCharWeight { get; init; } = 3;
        public bool TrimContentBeforeCount { get; init; } = true;
        public bool CountLeadingTrailingSpaces { get; init; } = true;
        public int LanguageDetectionSampleLength { get; init; } = 50;
        public bool DefaultLanguageIsPersian { get; init; } = true;
        public bool IncludeOptOutSuffixInCalculation { get; init; } = true;
        public string OptOutSuffix { get; init; } = "لغو11";

        public static SmsPartsRules Defaults { get; } = new();
    }

    /// <summary>
    /// اسنپ‌شات کامل تعرفه + قواعد برای مصرف در سرویس‌های ارسال.
    /// </summary>
    public sealed class SmsPricingRuntime
    {
        public decimal CostPerPart { get; init; } = 160m;

        /// <summary>آیا از نظر تنظیمات ادمین billing روشن است؟</summary>
        public bool IsBillingEnabled { get; init; }

        /// <summary>
        /// وضعیت مؤثر: ادمین روشن کرده و kill-switch سرور (DisableWalletCheck) خاموش است.
        /// </summary>
        public bool IsBillingEffectivelyEnabled { get; init; }

        public bool ServerWalletCheckDisabled { get; init; }

        public SmsPartsRules Rules { get; init; } = SmsPartsRules.Defaults;

        public static SmsPricingRuntime Defaults { get; } = new()
        {
            CostPerPart = 160m,
            IsBillingEnabled = false,
            IsBillingEffectivelyEnabled = false,
            ServerWalletCheckDisabled = true,
            Rules = SmsPartsRules.Defaults
        };
    }
}
