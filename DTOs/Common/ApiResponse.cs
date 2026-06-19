namespace Api_Vapp.DTOs.Common
{
    /// <summary>
    /// ساختار استاندارد پاسخ API
    /// </summary>
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public string? TraceId { get; set; }

        public static ApiResponse<T> CreateSuccess(T data, string message = "عملیات با موفقیت انجام شد", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> Error(string message, int statusCode = 400, List<string>? errors = null, string? errorCode = null)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Errors = errors
            };
        }

        public static ApiResponse<T> NotFound(string message = "منبع مورد نظر یافت نشد", string? errorCode = ErrorCodes.NotFound)
        {
            return Error(message, 404, errorCode: errorCode);
        }

        public static ApiResponse<T> Unauthorized(string message = "دسترسی غیرمجاز", string? errorCode = ErrorCodes.Unauthorized)
        {
            return Error(message, 401, errorCode: errorCode);
        }

        public static ApiResponse<T> Forbidden(string message = "شما مجاز به انجام این عملیات نیستید", string? errorCode = ErrorCodes.Forbidden)
        {
            return Error(message, 403, errorCode: errorCode);
        }

        public static ApiResponse<T> BadRequest(string message = "درخواست نامعتبر است", List<string>? errors = null, string? errorCode = ErrorCodes.InvalidInput)
        {
            return Error(message, 400, errors, errorCode);
        }

        public static ApiResponse<T> InternalServerError(string message = "خطای داخلی سرور. لطفاً با پشتیبانی تماس بگیرید.", string? errorCode = ErrorCodes.Unexpected)
        {
            return Error(message, 500, errorCode: errorCode);
        }
    }
}
