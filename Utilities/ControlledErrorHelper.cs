using System.Diagnostics;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// پیام‌های خطای کنترل‌شده — تنها منبع مجاز برای نمایش به کاربر
    /// </summary>
    public static class ControlledErrorHelper
    {
        public const string Unexpected = "خطای غیرمنتظره. لطفاً با پشتیبانی تماس بگیرید.";
        public const string InternalServer = "خطای داخلی سرور. لطفاً با پشتیبانی تماس بگیرید.";
        public const string Database = "مشکلی در ذخیره‌سازی اطلاعات پیش آمد. لطفاً دوباره تلاش کنید.";
        public const string Unauthorized = "شما مجاز به انجام این عملیات نیستید";
        public const string NotFound = "منبع مورد نظر یافت نشد";
        public const string BadRequest = "درخواست نامعتبر است";
        public const string InvalidInput = "اطلاعات وارد شده نامعتبر است";
        public const string PaymentFailed = "مشکلی در پردازش پرداخت پیش آمد. لطفاً دوباره تلاش کنید.";
        public const string SmsFailed = "مشکلی در ارسال پیامک پیش آمد. لطفاً دوباره تلاش کنید.";
        public const string FileUploadFailed = "مشکلی در آپلود فایل پیش آمد. لطفاً دوباره تلاش کنید.";
        public const string ExcelReadFailed = "مشکلی در خواندن فایل اکسل پیش آمد. لطفاً فرمت فایل را بررسی کنید.";
        public const string InactiveUserAccount = "حساب کاربری شما غیرفعال هست با پشتیبانی تماس بگیرید.";
        public const string AdminPanelAccessDenied = "شما دسترسی به پنل مدیریت ندارید. با پشتیبانی تماس بگیرید.";
        public const string InvalidToken = "توکن نامعتبر است. لطفاً دوباره وارد شوید.";
        public const string TokenProcessFailed = "خطا در پردازش توکن. لطفاً دوباره وارد شوید.";
        public const string LogoutFailed = "خطا در پردازش درخواست. لطفاً دوباره تلاش کنید.";
        public const string SendFailed = "خطا در ارسال پیام";
        public const string SystemError = "خطای سیستمی. لطفاً با پشتیبانی تماس بگیرید.";
        public const string OtpIncorrect = "کد تایید را اشتباه وارد کرده‌اید.";
        public const string OtpExpired = "کد تایید شما منقضی شده است.";

        /// <summary>
        /// آیا پیام از قبل توسط توسعه‌دهنده به فارسی و به‌صورت کنترل‌شده نوشته شده؟
        /// </summary>
        public static bool IsSafeUserMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var lower = message.ToLowerInvariant();

            if (lower.Contains("exception") || lower.Contains("stack") ||
                lower.Contains(" at ") || lower.Contains("sql") ||
                lower.Contains("timeout") || lower.Contains("null reference") ||
                lower.Contains("object reference") || lower.Contains("inner exception") ||
                lower.Contains('\\') || lower.Contains("wwwroot") || lower.Contains(":/"))
                return false;

            return message.Any(c => c is >= '\u0600' and <= '\u06FF');
        }

        public static string SanitizeArgumentMessage(string? message, string fallback = BadRequest)
        {
            return IsSafeUserMessage(message) ? message! : fallback;
        }

        public static string GetTraceId(HttpContext? context = null)
        {
            return Activity.Current?.Id ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        }
    }
}
