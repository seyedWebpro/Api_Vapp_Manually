namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// ارسال خودکار تبریک/تسلیت مناسبتی بر اساس جدول مناسبت‌های فعال هر کاربر
    /// </summary>
    public interface IOccasionGreetingDispatchService
    {
        /// <summary>
        /// مناسبت‌های امروز تهران را برای کاربرانی که زمان ارسالشان رسیده صف و ارسال می‌کند.
        /// </summary>
        /// <returns>تعداد گیرندگانی که در این اجرا صف شدند</returns>
        Task<int> DispatchDueGreetingsAsync(CancellationToken cancellationToken = default);
    }
}
