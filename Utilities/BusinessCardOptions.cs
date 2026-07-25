namespace Api_Vapp.Utilities
{
    public class BusinessCardOptions
    {
        public const string SectionName = "BusinessCard";

        /// <summary>
        /// پایه URL عمومی کارت ویزیت — مثال: https://ok-sms.ir/card
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
