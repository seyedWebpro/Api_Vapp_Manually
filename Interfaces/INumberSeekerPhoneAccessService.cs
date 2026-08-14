namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// دسترسی مشاهده شماره کامل شماره‌جو — فقط کاربران مجاز در پنل ادمین.
    /// </summary>
    public interface INumberSeekerPhoneAccessService
    {
        Task<bool> CanViewPhonesAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// شماره‌های مخفی دفترچه کاربر. اگر کاربر مجاز به مشاهده باشد، مجموعه خالی است.
        /// </summary>
        Task<HashSet<string>> GetHiddenMobileNumbersAsync(int userId, CancellationToken cancellationToken = default);
    }
}
