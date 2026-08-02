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
        /// <summary>
        /// منبع منتشرشده وجود دارد ولی برای دسترسی عمومی غیرفعال است (مثلاً فرم/گردونه)
        /// </summary>
        public const string ResourceInactive = "RESOURCE_INACTIVE";
        public const string DatabaseError = "DATABASE_ERROR";
        public const string TokenExpired = "TOKEN_EXPIRED";
        public const string TokenInvalid = "TOKEN_INVALID";
        public const string TokenProcessFailed = "TOKEN_PROCESS_FAILED";
        public const string LogoutFailed = "LOGOUT_FAILED";
        public const string SmsFailed = "SMS_FAILED";
        public const string PaymentFailed = "PAYMENT_FAILED";
        public const string FileUploadFailed = "FILE_UPLOAD_FAILED";
        public const string PushFailed = "PUSH_FAILED";
        public const string PushNotConfigured = "PUSH_NOT_CONFIGURED";
        public const string PushNoDevice = "PUSH_NO_DEVICE";
        public const string PushDisabled = "PUSH_DISABLED";
        public const string ReferralInvalid = "REFERRAL_INVALID";
        public const string ReferralDisabled = "REFERRAL_DISABLED";
        public const string ReferralSelfUse = "REFERRAL_SELF_USE";
        public const string OtpExpired = "OTP_EXPIRED";
        public const string OtpIncorrect = "OTP_INCORRECT";
        public const string OtpRateLimited = "OTP_RATE_LIMITED";
        public const string OtpLocked = "OTP_LOCKED";
    }
}
