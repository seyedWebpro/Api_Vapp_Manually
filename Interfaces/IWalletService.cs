using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس کیف پول
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// دریافت اطلاعات کیف پول کاربر
        /// </summary>
        Task<ApiResponse<WalletInfoDto>> GetWalletInfoAsync(int userId);

        /// <summary>
        /// دریافت تراکنش‌های کیف پول کاربر
        /// </summary>
        Task<ApiResponse<WalletTransactionListDto>> GetTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت آخرین تراکنش‌های کاربر
        /// </summary>
        Task<ApiResponse<List<WalletTransactionDto>>> GetRecentTransactionsAsync(int userId, int count = 5);

        /// <summary>
        /// درخواست شارژ کیف پول
        /// </summary>
        Task<ApiResponse<ChargeWalletResponseDto>> ChargeWalletAsync(int userId, ChargeWalletRequestDto request);

        /// <summary>
        /// اضافه کردن موجودی به کیف پول (استفاده داخلی)
        /// </summary>
        Task<ApiResponse<WalletTransactionDto>> AddBalanceAsync(int userId, decimal amount, string transactionType, string title, string? description = null, int? paymentId = null, int? cashbackId = null, string? referenceNumber = null);

        /// <summary>
        /// کسر موجودی از کیف پول (استفاده داخلی)
        /// </summary>
        Task<ApiResponse<WalletTransactionDto>> DeductBalanceAsync(int userId, decimal amount, string title, string? description = null);

        /// <summary>
        /// بررسی موجودی کافی
        /// </summary>
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);

        /// <summary>
        /// دریافت موجودی فعلی کاربر
        /// </summary>
        Task<decimal> GetBalanceAsync(int userId);

        /// <summary>
        /// دریافت اطلاعات کامل صفحه کیف پول
        /// شامل موجودی، کش‌بک‌های فعال و تاریخچه مالی
        /// </summary>
        Task<ApiResponse<WalletPageDto>> GetWalletPageAsync(int userId, int recentTransactionsCount = 10);
    }
}




