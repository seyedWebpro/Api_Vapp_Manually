using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای پرداخت
    /// </summary>
    public interface IPaymentRepository : IBaseRepository<Payment>
    {
        /// <summary>
        /// دریافت پرداخت بر اساس شماره سفارش
        /// </summary>
        Task<Payment?> GetByOrderIdAsync(string orderId);

        /// <summary>
        /// دریافت پرداخت بر اساس Authority / RefId درگاه
        /// </summary>
        Task<Payment?> GetByRefIdAsync(string refId);

        /// <summary>
        /// دریافت پرداخت‌های کاربر با صفحه‌بندی
        /// </summary>
        Task<IEnumerable<Payment>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت تعداد پرداخت‌های کاربر
        /// </summary>
        Task<int> GetCountByUserIdAsync(int userId);

        /// <summary>
        /// دریافت پرداخت‌های کاربر بر اساس وضعیت
        /// </summary>
        Task<IEnumerable<Payment>> GetByStatusAsync(int userId, string status);

        /// <summary>
        /// دریافت پرداخت‌های منقضی شده (برای پاکسازی)
        /// </summary>
        Task<IEnumerable<Payment>> GetExpiredPendingPaymentsAsync(TimeSpan timeout);

        /// <summary>
        /// بررسی وجود پرداخت در انتظار برای کاربر
        /// </summary>
        Task<bool> HasPendingPaymentAsync(int userId);

        /// <summary>
        /// منقضی‌کردن Pending/Processing قدیمی‌تر از timeout (قفل شارژ را باز می‌کند)
        /// </summary>
        Task ExpireStalePendingPaymentsAsync(TimeSpan timeout);

        /// <summary>
        /// لغو همه پرداخت‌های باز (Pending/Processing) کاربر تا بتواند درخواست جدید بزند
        /// (مثلاً بعد از بستن مرورگر درگاه). برمی‌گرداند تعداد ردیف‌های لغوشده.
        /// </summary>
        Task<int> AbandonOpenPaymentsForUserAsync(int userId, string reason);

        /// <summary>
        /// دریافت مجموع پرداخت‌های موفق کاربر
        /// </summary>
        Task<decimal> GetTotalSuccessfulPaymentsAsync(int userId);
    }
}




