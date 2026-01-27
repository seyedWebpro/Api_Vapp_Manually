namespace Api_Vapp.DTOs.Auth
{
    /// <summary>
    /// DTO برای ذخیره اطلاعات تلاش‌های OTP در کش
    /// </summary>
    public class OtpAttemptCacheDto
    {
        public int AttemptCount { get; set; }
        public DateTime FirstAttemptTime { get; set; }
        public DateTime? LockedUntil { get; set; }
    }
}

