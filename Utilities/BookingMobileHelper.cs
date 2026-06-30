namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی و اعتبارسنجی شماره موبایل برای رزرو
    /// </summary>
    public static class BookingMobileHelper
    {
        public static string Normalize(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return string.Empty;
            }

            var value = mobile.Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty);

            if (value.StartsWith("+98"))
            {
                value = "0" + value[3..];
            }
            else if (value.StartsWith("98") && value.Length == 12)
            {
                value = "0" + value[2..];
            }
            else if (value.StartsWith('9') && value.Length == 10)
            {
                value = "0" + value;
            }

            return value;
        }

        public static bool IsValidIranianMobile(string? mobile)
        {
            var normalized = Normalize(mobile);
            return normalized.Length == 11 &&
                   normalized.StartsWith("09") &&
                   normalized.All(char.IsDigit);
        }
    }
}
