namespace Api_Vapp.Exceptions
{
    /// <summary>
    /// Exception کنترل‌شده با پیام امن برای کاربر
    /// </summary>
    public class AppException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }
        public List<string>? Errors { get; }

        public AppException(string errorCode, string message, int statusCode = 400, List<string>? errors = null, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            Errors = errors;
        }

        public static AppException BadRequest(string errorCode, string message, List<string>? errors = null) =>
            new(errorCode, message, 400, errors);

        public static AppException Unauthorized(string errorCode, string message) =>
            new(errorCode, message, 401);

        public static AppException Forbidden(string errorCode, string message) =>
            new(errorCode, message, 403);

        public static AppException NotFound(string errorCode, string message) =>
            new(errorCode, message, 404);

        public static AppException Internal(string errorCode, string message) =>
            new(errorCode, message, 500);
    }
}
