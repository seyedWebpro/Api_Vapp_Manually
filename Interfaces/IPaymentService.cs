using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس پرداخت
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// ایجاد پرداخت جدید
        /// </summary>
        Task<ApiResponse<PaymentDto>> CreatePaymentAsync(int userId, CreatePaymentDto createDto);

        /// <summary>
        /// دریافت پرداخت بر اساس شناسه
        /// </summary>
        Task<ApiResponse<PaymentDto>> GetPaymentByIdAsync(int id, int userId);

        /// <summary>
        /// دریافت پرداخت بر اساس شماره سفارش
        /// </summary>
        Task<ApiResponse<PaymentDto>> GetPaymentByOrderIdAsync(string orderId, int userId);

        /// <summary>
        /// تأیید پرداخت (Verify)
        /// </summary>
        Task<ApiResponse<PaymentResultDto>> VerifyPaymentAsync(int userId, VerifyPaymentRequestDto verifyDto);

        /// <summary>
        /// دریافت لیست پرداخت‌های کاربر
        /// </summary>
        Task<ApiResponse<PaymentListDto>> GetPaymentsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت درگاه‌های پرداخت فعال
        /// </summary>
        Task<ApiResponse<List<PaymentGatewayInfoDto>>> GetAvailableGatewaysAsync();

        /// <summary>
        /// لغو پرداخت منتظر
        /// </summary>
        Task<ApiResponse<bool>> CancelPaymentAsync(int paymentId, int userId);

        /// <summary>
        /// درخواست توکن از درگاه به‌پرداخت
        /// </summary>
        Task<(bool Success, string? RefId, string? ErrorMessage)> RequestBehpardakhtTokenAsync(int paymentId, decimal amount, string orderId, string callbackUrl);

        /// <summary>
        /// تأیید و تسویه پرداخت از درگاه به‌پرداخت
        /// </summary>
        Task<(bool Success, string? SaleReferenceId, string? ErrorMessage)> VerifyAndSettleBehpardakhtAsync(string refId, long saleReferenceId);
    }
}




