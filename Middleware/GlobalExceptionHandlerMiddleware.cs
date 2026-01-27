using Api_Vapp.DTOs.Common;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Middleware
{
    /// <summary>
    /// Middleware برای مدیریت خطاهای سراسری و برگرداندن Response استاندارد
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred. Path: {Path}", context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// ترجمه خطاهای انگلیسی به فارسی
        /// </summary>
        private static string TranslateError(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return "خطا در پردازش درخواست";

            // اگر خطا قبلاً فارسی است، همان را برمی‌گردانیم
            if (errorMessage.Contains("الزامی") || errorMessage.Contains("صحیح نیست") || 
                errorMessage.Contains("نمی‌تواند") || errorMessage.Contains("باید") ||
                errorMessage.Contains("نامعتبر") || errorMessage.Contains("یافت نشد"))
            {
                return errorMessage;
            }

            var errorLower = errorMessage.ToLower();

            // خطاهای مربوط به Required/Not Null
            if (errorLower.Contains("cannot insert null") || errorLower.Contains("cannot be null") || 
                errorLower.Contains("required") || errorLower.Contains("not null") ||
                errorLower.Contains("is required"))
            {
                if (errorLower.Contains("fullname") || errorLower.Contains("full name"))
                    return "نام خانوادگی الزامی است";
                if (errorLower.Contains("mobilenumber") || errorLower.Contains("mobile number"))
                    return "شماره موبایل الزامی است";
                if (errorLower.Contains("contactnotebookid") || errorLower.Contains("contact notebook"))
                    return "شناسه دفترچه الزامی است";
                if (errorLower.Contains("password"))
                    return "رمز عبور الزامی است";
                if (errorLower.Contains("email"))
                    return "ایمیل الزامی است";
                return "برخی فیلدهای الزامی وارد نشده‌اند";
            }

            // خطاهای مربوط به Unique Constraint
            if (errorLower.Contains("unique") || errorLower.Contains("duplicate") || 
                errorLower.Contains("violation of unique key") || errorLower.Contains("already exists"))
            {
                if (errorLower.Contains("mobilenumber") || errorLower.Contains("mobile number"))
                    return "مخاطبی با این شماره موبایل قبلاً ثبت شده است";
                if (errorLower.Contains("email"))
                    return "ایمیل تکراری است";
                if (errorLower.Contains("username"))
                    return "نام کاربری تکراری است";
                return "اطلاعات تکراری است";
            }

            // خطاهای مربوط به Foreign Key
            if (errorLower.Contains("foreign key") || errorLower.Contains("reference") ||
                errorLower.Contains("constraint") || errorLower.Contains("referenced"))
            {
                return "اطلاعات مرتبط یافت نشد";
            }

            // خطاهای مربوط به Length/MaxLength
            if (errorLower.Contains("length") || errorLower.Contains("maximum") || 
                errorLower.Contains("exceeds") || errorLower.Contains("too long"))
            {
                return "طول داده وارد شده بیش از حد مجاز است";
            }

            // خطاهای مربوط به Format
            if (errorLower.Contains("format") || errorLower.Contains("invalid format") ||
                errorLower.Contains("invalid"))
            {
                if (errorLower.Contains("mobile") || errorLower.Contains("phone"))
                    return "فرمت شماره موبایل صحیح نیست";
                if (errorLower.Contains("email"))
                    return "فرمت ایمیل صحیح نیست";
                if (errorLower.Contains("date"))
                    return "فرمت تاریخ صحیح نیست";
                return "فرمت داده وارد شده صحیح نیست";
            }

            // خطاهای مربوط به Range/Min/Max
            if (errorLower.Contains("range") || errorLower.Contains("minimum") || 
                errorLower.Contains("maximum") || errorLower.Contains("out of range"))
            {
                return "مقدار وارد شده خارج از محدوده مجاز است";
            }

            // خطاهای ArgumentException
            if (errorLower.Contains("argument") || errorLower.Contains("parameter"))
            {
                // اگر پیام مشخص دارد، آن را برمی‌گردانیم
                if (errorMessage.Length > 10 && !errorLower.Contains("argument") && !errorLower.Contains("parameter"))
                {
                    return errorMessage; // احتمالاً پیام فارسی است
                }
                return "پارامتر وارد شده نامعتبر است";
            }

            // برای سایر خطاها، پیام پیش‌فرض
            return "خطا در پردازش درخواست";
        }

        /// <summary>
        /// ترجمه خطاهای دیتابیس (برای سازگاری با کد قبلی)
        /// </summary>
        private static string TranslateDatabaseError(string errorMessage)
        {
            return TranslateError(errorMessage);
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var statusCode = exception switch
            {
                ArgumentNullException => HttpStatusCode.BadRequest,
                ArgumentException => HttpStatusCode.BadRequest,
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                DbUpdateException => HttpStatusCode.BadRequest, // خطاهای دیتابیس
                _ => HttpStatusCode.InternalServerError
            };

            response.StatusCode = (int)statusCode;

            string errorMessage;
            List<string>? errors = null;

            // هندل کردن خطاهای دیتابیس
            if (exception is DbUpdateException dbEx)
            {
                errorMessage = TranslateError(dbEx.InnerException?.Message ?? dbEx.Message);
                errors = new List<string> { errorMessage };
                
                // اگر خطای داخلی وجود دارد، آن را هم اضافه می‌کنیم
                if (dbEx.InnerException != null && !string.IsNullOrWhiteSpace(dbEx.InnerException.Message))
                {
                    var innerError = TranslateError(dbEx.InnerException.Message);
                    if (innerError != errorMessage)
                    {
                        errors.Add(innerError);
                    }
                }
            }
            else if (statusCode == HttpStatusCode.InternalServerError)
            {
                errorMessage = "خطای داخلی سرور";
                errors = null; // برای خطاهای 500، errors را null نگه می‌داریم
            }
            else if (exception is ArgumentException || exception is ArgumentNullException)
            {
                // برای ArgumentException ها، پیام را ترجمه می‌کنیم
                errorMessage = TranslateError(exception.Message);
                errors = new List<string> { errorMessage };
            }
            else if (exception is KeyNotFoundException)
            {
                errorMessage = TranslateError(exception.Message);
                if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage == "خطا در پردازش درخواست")
                {
                    errorMessage = "منبع مورد نظر یافت نشد";
                }
                errors = new List<string> { errorMessage };
            }
            else if (exception is UnauthorizedAccessException)
            {
                errorMessage = TranslateError(exception.Message);
                if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage == "خطا در پردازش درخواست")
                {
                    errorMessage = "شما مجاز به انجام این عملیات نیستید";
                }
                errors = new List<string> { errorMessage };
            }
            else
            {
                // تبدیل خطاهای انگلیسی به فارسی برای سایر exception ها
                errorMessage = TranslateError(exception.Message);
                errors = new List<string> { errorMessage };
            }

            var errorResponse = new ApiResponse<object>
            {
                StatusCode = (int)statusCode,
                Success = false,
                Message = errorMessage,
                Errors = errors
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return response.WriteAsync(json);
        }
    }
}

