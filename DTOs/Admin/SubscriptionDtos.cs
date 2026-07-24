using System.ComponentModel.DataAnnotations;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.Admin
{
    public class SubscriptionFeatureSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class SubscriptionFeatureResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemManaged { get; set; }
        public bool CanChangeCode { get; set; }
        public bool CanDelete { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateSubscriptionFeatureDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSubscriptionFeatureDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SubscriptionPlanResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public bool FreeQuickSendEnabled { get; set; }
        public bool BusinessCardEnabled { get; set; }
        public int? MonthlySmsLimit { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>کد سطح سیستمی است و قابل تغییر نیست.</summary>
        public bool IsSystemTier { get; set; }
        /// <summary>پلن رایگان سیستمی — حذف و غیرفعال‌سازی ممنوع.</summary>
        public bool IsFreeTier { get; set; }
        public bool CanChangeTierCode { get; set; }
        public bool CanDelete { get; set; }
        public bool CanDeactivate { get; set; }
        public List<int> FeatureIds { get; set; } = new();
        public List<SubscriptionFeatureSummaryDto> Features { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateSubscriptionPlanDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TierCode { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(1, 3650)]
        public int DurationDays { get; set; } = 30;

        public List<int> FeatureIds { get; set; } = new();
        public int? MonthlySmsLimit { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSubscriptionPlanDto : CreateSubscriptionPlanDto
    {
    }

    public class UserSubscriptionResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserFullName { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AssignUserSubscriptionDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int SubscriptionPlanId { get; set; }

        public DateTime? StartDate { get; set; }
    }

    public class SubscriptionDiscountCodeResponseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string DiscountType { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public int? SubscriptionPlanId { get; set; }
        public string? SubscriptionPlanName { get; set; }
        public int? MaxTotalUses { get; set; }
        public int UsedCount { get; set; }
        public int? MaxUsesPerUser { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateSubscriptionDiscountCodeDto
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Title { get; set; }

        [Required]
        public string DiscountType { get; set; } = SubscriptionDiscountTypes.Fixed;

        [Range(0, double.MaxValue)]
        public decimal Value { get; set; }

        public decimal? MaxDiscountAmount { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public int? SubscriptionPlanId { get; set; }
        public int? MaxTotalUses { get; set; }
        public int? MaxUsesPerUser { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSubscriptionDiscountCodeDto : CreateSubscriptionDiscountCodeDto
    {
    }
}
