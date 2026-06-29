namespace Api_Vapp.Utilities
{
    public class LuckyWheelOptions
    {
        public const string SectionName = "LuckyWheel";

        /// <summary>
        /// پایه URL عمومی گردونه — مثال: https://app.com/wheel
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
