namespace Api_Vapp.Utilities
{
    public class BookingSystemOptions
    {
        public const string SectionName = "BookingSystem";

        /// <summary>
        /// پایه URL عمومی رزرو — مثال: https://app.com/book
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
