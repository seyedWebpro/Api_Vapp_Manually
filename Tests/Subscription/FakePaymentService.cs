using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;

namespace Api_Vapp.Tests.Subscription;

internal sealed class FakePaymentService : IPaymentService
{
    private readonly Api_Context _context;

    public FakePaymentService(Api_Context context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PaymentDto>> CreatePaymentAsync(int userId, CreatePaymentDto createDto)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return ApiResponse<PaymentDto>.NotFound("کاربر یافت نشد");

        var payment = new Payment
        {
            UserId = userId,
            Amount = createDto.Amount,
            PaymentType = createDto.PaymentType,
            Gateway = createDto.Gateway,
            OrderId = $"TST{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
            Status = PaymentStatuses.Pending,
            Description = createDto.Description,
            CallbackUrl = createDto.CallbackUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return ApiResponse<PaymentDto>.CreateSuccess(Map(payment), "پرداخت با موفقیت ایجاد شد", 201);
    }

    public Task<ApiResponse<List<PaymentGatewayInfoDto>>> GetAvailableGatewaysAsync()
    {
        var gateways = new List<PaymentGatewayInfoDto>
        {
            new()
            {
                Code = PaymentGateways.Behpardakht,
                Name = "به‌پرداخت",
                Description = "پرداخت از طریق درگاه بانکی به‌پرداخت",
                IsActive = true,
                ComingSoon = false
            },
            new()
            {
                Code = PaymentGateways.Wallet,
                Name = "پرداخت درون برنامه‌ای",
                Description = "امکان پرداخت مستقیم از داخل اپ",
                IsActive = false,
                ComingSoon = true
            }
        };

        return Task.FromResult(ApiResponse<List<PaymentGatewayInfoDto>>.CreateSuccess(gateways));
    }

    public Task<(bool Success, string? RefId, string? ErrorMessage)> RequestBehpardakhtTokenAsync(
        int paymentId,
        decimal amount,
        string orderId,
        string callbackUrl) =>
        Task.FromResult<(bool, string?, string?)>((true, $"SIMREF{paymentId}", null));

    public Task<ApiResponse<PaymentDto>> GetPaymentByIdAsync(int id, int userId) =>
        throw new NotSupportedException();

    public Task<ApiResponse<PaymentDto>> GetPaymentByOrderIdAsync(string orderId, int userId) =>
        throw new NotSupportedException();

    public Task<ApiResponse<PaymentResultDto>> VerifyPaymentAsync(int userId, VerifyPaymentRequestDto verifyDto) =>
        throw new NotSupportedException();

    public Task<ApiResponse<PaymentListDto>> GetPaymentsAsync(int userId, int pageNumber = 1, int pageSize = 10) =>
        throw new NotSupportedException();

    public Task<ApiResponse<bool>> CancelPaymentAsync(int paymentId, int userId) =>
        throw new NotSupportedException();

    public Task<(bool Success, string? SaleReferenceId, string? ErrorMessage)> VerifyAndSettleBehpardakhtAsync(
        string refId,
        long saleReferenceId) =>
        Task.FromResult<(bool, string?, string?)>((true, saleReferenceId.ToString(), null));

    private static PaymentDto Map(Payment payment) => new()
    {
        Id = payment.Id,
        Amount = payment.Amount,
        FormattedAmount = $"{payment.Amount:N0} تومان",
        PaymentType = payment.PaymentType,
        Gateway = payment.Gateway,
        OrderId = payment.OrderId,
        Status = payment.Status,
        CreatedAt = payment.CreatedAt
    };
}
