using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
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

        public bool FreeQuickSendEnabled { get; set; }
        public bool BusinessCardEnabled { get; set; }
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
}
