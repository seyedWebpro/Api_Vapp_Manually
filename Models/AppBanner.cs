namespace Api_Vapp.Models
{
    /// <summary>
    /// بنر نمایشی اپ موبایل — اسلات‌های سیستمی (مثلاً home) از seed می‌آیند و در پنل ادمین ویرایش می‌شوند.
    /// </summary>
    public class AppBanner
    {
        public int Id { get; set; }

        /// <summary>کلید پایدار اسلات (مثلاً home، tool) — اپ با این کلید بنر را پیدا می‌کند.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>نام نمایشی در پنل ادمین.</summary>
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>مسیر نسبی تصویر آپلودشده (مثلاً /uploads/appbanner/1/images/...).</summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// مقصد کلیک: مسیر داخلی اپ (مثلاً /CreateWheelOfFortune) یا URL خارجی https://...
        /// </summary>
        public string? LinkUrl { get; set; }

        /// <summary>none | app_route | external_url — مقادیر: AppBannerLinkTypes</summary>
        public string LinkType { get; set; } = "none";

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
