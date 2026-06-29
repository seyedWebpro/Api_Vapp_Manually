using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface ISubscriptionDiscountService
    {
        Task<ApiResponse<List<SubscriptionDiscountCodeResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> CreateAsync(CreateSubscriptionDiscountCodeDto dto);
        Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> UpdateAsync(int id, UpdateSubscriptionDiscountCodeDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<(SubscriptionDiscountCode? Discount, decimal DiscountAmount, string? ErrorMessage)> CalculateAsync(
            int userId,
            int planId,
            decimal planPrice,
            string? discountCode);
    }

    public interface ISubscriptionPurchaseService
    {
        Task<ApiResponse<SubscriptionCheckoutDto>> GetCheckoutPreviewAsync(int userId, SubscriptionCheckoutPreviewRequest request);
        Task<ApiResponse<SubscriptionPurchaseResultDto>> InitiatePurchaseAsync(int userId, SubscriptionPurchaseRequest request);
    }
}
