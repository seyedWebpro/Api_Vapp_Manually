using Microsoft.Extensions.Logging;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// فقط Development — نمایش کد OTP در ترمینال/لاگ برای تست محلی.
    /// در Production با <c>enabled: false</c> هیچ خروجی‌ای تولید نمی‌شود.
    /// </summary>
    public static class DevOtpLogger
    {
        public static void Write(ILogger logger, string phoneNumber, string otpCode, string purpose, bool enabled = true)
        {
            if (!enabled)
                return;

            const string border = "==================================================";

            Console.WriteLine();
            Console.WriteLine(border);
            Console.WriteLine("  DEV OTP — فقط محیط توسعه");
            Console.WriteLine(border);
            Console.WriteLine($"  {otpCode}");
            Console.WriteLine(border);
            Console.WriteLine($"  Phone: {phoneNumber}  |  Type: {purpose}");
            Console.WriteLine(border);
            Console.WriteLine();

            logger.LogWarning(
                "DEV OTP issued | Phone: {PhoneNumber} | Type: {Purpose}",
                phoneNumber, purpose);
        }
    }
}
