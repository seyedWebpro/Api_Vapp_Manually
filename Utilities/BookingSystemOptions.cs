namespace Api_Vapp.Utilities
{
    public class BookingSystemOptions
    {
        public const string SectionName = "BookingSystem";

        /// <summary>
        /// پایه URL عمومی رزرو — مثال: https://app.com/book
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// بازه مجاز رزرو عمومی از امروز UTC (روز) — مثال: 7 یعنی فقط 7 روز آینده.
        /// </summary>
        public int PublicBookingWindowDays { get; set; } = 7;
    }
}
