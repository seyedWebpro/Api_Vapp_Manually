using Microsoft.Extensions.Logging;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// DEV ONLY — نمایش کد OTP در ترمینال/لاگ سرور برای تست محلی.
    /// TODO(production): قبل از آماده‌سازی سورس برای کاربران واقعی، کل این فایل را حذف کنید.
    /// جستجو در پروژه: DevOtpLogger
    /// </summary>
    public static class DevOtpLogger
    {
        public static void Write(ILogger logger, string phoneNumber, string otpCode, string purpose)
        {
            const string border = "==================================================";

            // بنر در ترمینال — کد OTP تنها روی یک خط برای copy/paste آسان
            Console.WriteLine();
            Console.WriteLine(border);
            Console.WriteLine("  DEV OTP — فقط محیط توسعه (حذف قبل از production)");
            Console.WriteLine(border);
            Console.WriteLine($"  {otpCode}");
            Console.WriteLine(border);
            Console.WriteLine($"  Phone: {phoneNumber}  |  Type: {purpose}");
            Console.WriteLine(border);
            Console.WriteLine();

            logger.LogWarning(
                "DEV OTP >>> {OtpCode} <<< | Phone: {PhoneNumber} | Type: {Purpose}",
                otpCode, phoneNumber, purpose);
        }
    }
}
