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
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

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

        public static ApiResponse<T> Error(string message, int statusCode = 400, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                Success = false,
                Message = message,
                Errors = errors
            };
        }

        public static ApiResponse<T> NotFound(string message = "منبع مورد نظر یافت نشد")
        {
            return Error(message, 404);
        }

        public static ApiResponse<T> Unauthorized(string message = "دسترسی غیرمجاز")
        {
            return Error(message, 401);
        }

        public static ApiResponse<T> Forbidden(string message = "شما مجاز به انجام این عملیات نیستید")
        {
            return Error(message, 403);
        }

        public static ApiResponse<T> BadRequest(string message = "درخواست نامعتبر است", List<string>? errors = null)
        {
            return Error(message, 400, errors);
        }

        public static ApiResponse<T> InternalServerError(string message = "خطای داخلی سرور")
        {
            return Error(message, 500);
        }
    }
}

