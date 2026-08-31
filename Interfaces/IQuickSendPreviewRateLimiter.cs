namespace Api_Vapp.Interfaces
{
    /// <summary>محدودیت نرخ برای endpoint پیش‌نمایش توکن‌دار — جلوگیری از brute force.</summary>
    public interface IQuickSendPreviewRateLimiter
    {
        /// <summary>خواندن محتوا با توکن — per IP</summary>
        Task<(bool Allowed, int? RetryAfterSeconds)> CheckPreviewReadAsync(string clientKey);

        Task RecordPreviewReadAsync(string clientKey);

        /// <summary>صدور توکن — per ادمین</summary>
        Task<(bool Allowed, int? RetryAfterSeconds)> CheckTokenIssueAsync(int adminUserId);

        Task RecordTokenIssueAsync(int adminUserId);
    }
}
