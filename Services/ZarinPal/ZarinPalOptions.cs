namespace Api_Vapp.Services.ZarinPal
{
    /// <summary>
    /// تنظیمات درگاه زرین‌پال (از بخش ZarinPal در appsettings)
    /// </summary>
    public class ZarinPalOptions
    {
        public const string SectionName = "ZarinPal";

        /// <summary>مرچنت‌آیدی ۳۶ کاراکتری زرین‌پال</summary>
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>
        /// آدرس Callback سمت سرور که زرین‌پال بعد از پرداخت به آن برمی‌گردد.
        /// مثال: https://v-application.ir/api/Payment/callback/zarinpal
        /// </summary>
        public string CallbackUrl { get; set; } = string.Empty;

        /// <summary>حالت آزمایشی (Sandbox)</summary>
        public bool Sandbox { get; set; }

        /// <summary>واحد پول ارسالی به زرین‌پال — IRT (تومان) یا IRR (ریال)</summary>
        public string Currency { get; set; } = "IRT";

        /// <summary>
        /// Deep Link بازگشت به اپ بعد از Callback سرور.
        /// مثال: vapp://payment/result
        /// </summary>
        public string AppReturnUrl { get; set; } = "vapp://payment/result";

        /// <summary>
        /// آدرس وب اختیاری برای نمایش نتیجه (اگر Deep Link در دسترس نباشد)
        /// </summary>
        public string? FrontendCallbackUrl { get; set; }

        /// <summary>
        /// فقط برای تست خودکار سندباکس: Verify بدون پرداخت واقعی موفق شود.
        /// هرگز در Production روشن نشود. فقط وقتی Sandbox=true اثر دارد.
        /// </summary>
        public bool AllowSandboxAutoVerify { get; set; }
    }
}
