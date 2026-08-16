namespace Api_Vapp.Configuration
{
    /// <summary>
    /// تنظیمات اتصال به وب‌سرویس زحل (شاهکار و سایر استعلام‌ها)
    /// </summary>
    public class ZohalApiSettings
    {
        public const string SectionName = "Zohal";

        /// <summary>فعال/غیرفعال بودن اتصال به زحل</summary>
        public bool Enabled { get; set; }

        /// <summary>آدرس پایه API — پیش‌فرض: https://service.zohal.io/api/v0</summary>
        public string BaseUrl { get; set; } = "https://service.zohal.io/api/v0";

        /// <summary>توکن Bearer از پنل زحل (توسعه‌دهندگان)</summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>مهلت درخواست HTTP به زحل (ثانیه)</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// اگر Enabled=false باشد، تطبیق شاهکار رد نشود (فقط برای توسعه محلی).
        /// در Production همیشه Enabled=true باشد.
        /// </summary>
        public bool SkipVerificationWhenDisabled { get; set; } = true;
    }
}
