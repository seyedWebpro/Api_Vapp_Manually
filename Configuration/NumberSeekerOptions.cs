namespace Api_Vapp.Configuration
{
    /// <summary>
    /// تنظیمات ماژول شماره‌جو (rate limit، import).
    /// </summary>
    public class NumberSeekerOptions
    {
        public const string SectionName = "NumberSeeker";

        /// <summary>حداکثر تعداد اسکرپ در ساعت برای هر کاربر</summary>
        public int MaxScrapesPerHour { get; set; } = 10;

        /// <summary>حداکثر تعداد import در ساعت برای هر کاربر</summary>
        public int MaxImportsPerHour { get; set; } = 20;

        /// <summary>پیش‌فرض نام مخاطب هنگام import</summary>
        public string DefaultContactNamePrefix { get; set; } = "مخاطب";
    }
}
