namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس مدیریت Blacklist برای توکن‌های JWT لغو شده
    /// </summary>
    public interface ITokenBlacklistService
    {
        /// <summary>
        /// اضافه کردن JTI (JWT ID) به blacklist
        /// </summary>
        /// <param name="jti">شناسه یکتای توکن (JTI)</param>
        /// <param name="expirationMinutes">زمان انقضای توکن به دقیقه (برای حذف خودکار از cache)</param>
        Task AddToBlacklistAsync(string jti, int expirationMinutes);

        /// <summary>
        /// بررسی اینکه آیا JTI در blacklist است یا نه
        /// </summary>
        /// <param name="jti">شناسه یکتای توکن (JTI)</param>
        /// <returns>true اگر توکن در blacklist باشد</returns>
        Task<bool> IsTokenBlacklistedAsync(string jti);

        /// <summary>
        /// حذف JTI از blacklist (در صورت نیاز)
        /// </summary>
        /// <param name="jti">شناسه یکتای توکن (JTI)</param>
        Task RemoveFromBlacklistAsync(string jti);
    }
}






