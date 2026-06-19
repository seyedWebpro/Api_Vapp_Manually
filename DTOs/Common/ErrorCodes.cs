namespace Api_Vapp.DTOs.Common
{
    /// <summary>
    /// کدهای خطای استاندارد — برای استفاده فرانت و پشتیبانی
    /// </summary>
    public static class ErrorCodes
    {
        public const string Unexpected = "UNEXPECTED_ERROR";
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string InvalidInput = "INVALID_INPUT";
        public const string InvalidUserId = "INVALID_USER_ID";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string DatabaseError = "DATABASE_ERROR";
        public const string TokenExpired = "TOKEN_EXPIRED";
        public const string TokenInvalid = "TOKEN_INVALID";
        public const string TokenProcessFailed = "TOKEN_PROCESS_FAILED";
        public const string LogoutFailed = "LOGOUT_FAILED";
        public const string SmsFailed = "SMS_FAILED";
        public const string PaymentFailed = "PAYMENT_FAILED";
        public const string FileUploadFailed = "FILE_UPLOAD_FAILED";
    }
}
