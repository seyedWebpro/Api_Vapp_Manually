namespace Api_Vapp.DTOs.Auth
{
    /// <summary>
    /// DTO برای پاسخ عملیات خروج از سیستم
    /// </summary>
    public class LogoutResponseDto
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>برای پیگیری پشتیبانی</summary>
        public string? TraceId { get; set; }
    }
}






