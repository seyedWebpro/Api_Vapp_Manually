using System.ComponentModel.DataAnnotations;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.ReferralProgram
{
    #region Response DTOs

    public class ReferralProgramDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string RewardType { get; set; } = string.Empty;
        public decimal ReferrerRewardValue { get; set; }
        public string FormattedReferrerReward { get; set; } = string.Empty;
        public bool IsCustomerRewardActive { get; set; }
        public decimal? CustomerRewardValue { get; set; }
        public string? FormattedCustomerReward { get; set; }
        public string PublicCode { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string AudienceDescription { get; set; } = string.Empty;
        public List<int>? TargetNotebookIds { get; set; }
        public List<int>? TargetContactIds { get; set; }
        public List<int>? TargetTagIds { get; set; }
        public bool SendToSpecificTags { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int NotifiedContactsCount { get; set; }
        public bool IsCurrentlyValid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ReferralProgramListDto
    {
        public List<ReferralProgramDto> Programs { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class ReferralDashboardStatsDto
    {
        public int SuccessfulReferrals { get; set; }
        public decimal TotalRewardsPaid { get; set; }
        public string FormattedTotalRewardsPaid { get; set; } = "0 تومان";
        public int ActiveProgramsCount { get; set; }

        /// <summary>
        /// تعداد مخاطبین یکتا که در مصرف کدها نقش داشته‌اند
        /// </summary>
        public int ActiveUsersCount { get; set; }
    }

    public class ReferralNotebookDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MembersCount { get; set; }
    }

    public class ReferralStep1ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? DraftId { get; set; }
        public DateTime? DraftExpiresAt { get; set; }
    }

    public class ReferralStep2ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public int TotalContactsCount { get; set; }
        public string TargetAudienceDescription { get; set; } = string.Empty;
    }

    public class ReferralSummaryDto
    {
        public string ProgramTitle { get; set; } = string.Empty;
        public string RewardType { get; set; } = string.Empty;
        public string ReferrerReward { get; set; } = string.Empty;
        public string CustomerReward { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ContactsCount { get; set; }
    }

    public class ConfirmReferralProgramResponseDto
    {
        public ReferralProgramDto Program { get; set; } = null!;
        public int SmsSentCount { get; set; }
        public int SmsFailedCount { get; set; }
    }

    public class InquireReferralCodeResponseDto
    {
        public bool IsValid { get; set; }
        public bool IsExpired { get; set; }
        public bool IsNotStarted { get; set; }
        public bool IsActive { get; set; }
        public string? InvalidReason { get; set; }
        public int? ProgramId { get; set; }
        public string? ProgramName { get; set; }
        public string? PublicCode { get; set; }
        public string? RewardType { get; set; }
        public bool IsCustomerRewardActive { get; set; }
        public decimal? CustomerDiscountAmount { get; set; }
        public string? FormattedCustomerDiscount { get; set; }
        public decimal? ReferrerRewardValue { get; set; }
        public string? FormattedReferrerReward { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ReferralUsageDto
    {
        public int Id { get; set; }
        public int ReferralProgramId { get; set; }
        public string ProgramTitle { get; set; } = string.Empty;
        public string PublicCode { get; set; } = string.Empty;
        public decimal? PurchaseAmount { get; set; }
        public string? FormattedPurchaseAmount { get; set; }
        public decimal CustomerDiscountAmount { get; set; }
        public string FormattedCustomerDiscount { get; set; } = string.Empty;
        public decimal ReferrerRewardAmount { get; set; }
        public string FormattedReferrerReward { get; set; } = string.Empty;
        public int? CustomerContactId { get; set; }
        public string? CustomerContactName { get; set; }
        public string? CustomerContactMobile { get; set; }
        public int? ReferrerContactId { get; set; }
        public string? ReferrerContactName { get; set; }
        public string? ReferrerContactMobile { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReferralUsageHistoryListDto
    {
        public List<ReferralUsageDto> Usages { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public decimal TotalCustomerDiscount { get; set; }
        public string FormattedTotalCustomerDiscount { get; set; } = string.Empty;
        public decimal TotalReferrerReward { get; set; }
        public string FormattedTotalReferrerReward { get; set; } = string.Empty;
    }

    public class RedeemReferralCodeResponseDto
    {
        public int UsageId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string PublicCode { get; set; } = string.Empty;
        public decimal? PurchaseAmount { get; set; }
        public decimal CustomerDiscountAmount { get; set; }
        public string FormattedCustomerDiscount { get; set; } = string.Empty;
        public decimal ReferrerRewardAmount { get; set; }
        public string FormattedReferrerReward { get; set; } = string.Empty;
        public bool CustomerRewardCredited { get; set; }
        public bool ReferrerRewardCredited { get; set; }
    }

    #endregion

    #region Request DTOs

    public class ReferralStep1Dto
    {
        [Required(ErrorMessage = "نام برنامه پاداش الزامی است")]
        [MaxLength(200, ErrorMessage = "نام برنامه حداکثر 200 کاراکتر است")]
        public string Title { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "نوع پاداش الزامی است")]
        public string RewardType { get; set; } = ReferralRewardTypes.Percentage;

        [Range(0.01, 100000000, ErrorMessage = "مقدار پاداش معرف نامعتبر است")]
        public decimal ReferrerRewardValue { get; set; }

        public bool IsCustomerRewardActive { get; set; }

        public decimal? CustomerRewardValue { get; set; }
    }

    public class ReferralStep2Dto
    {
        public string? DraftId { get; set; }

        [Required(ErrorMessage = "نوع مخاطبین الزامی است")]
        public string TargetAudience { get; set; } = ReferralTargetAudience.All;

        public List<int>? TargetNotebookIds { get; set; }

        public List<int>? TargetContactIds { get; set; }
    }

    public class SaveReferralStep3SettingsDto
    {
        [Required(ErrorMessage = "تاریخ شروع الزامی است")]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool SendToSpecificTags { get; set; }

        public List<int>? TargetTagIds { get; set; }
    }

    public class SaveReferralStep3RequestDto
    {
        [Required(ErrorMessage = "شناسه پیش‌نویس الزامی است")]
        public string DraftId { get; set; } = string.Empty;

        [Required]
        public SaveReferralStep3SettingsDto Settings { get; set; } = null!;
    }

    public class GetReferralSummaryRequestDto
    {
        [Required(ErrorMessage = "شناسه پیش‌نویس الزامی است")]
        public string DraftId { get; set; } = string.Empty;
    }

    public class ConfirmReferralProgramDto
    {
        [Required(ErrorMessage = "شناسه پیش‌نویس الزامی است")]
        public string DraftId { get; set; } = string.Empty;
    }

    public class InquireReferralCodeDto
    {
        [Required(ErrorMessage = "کد معرف الزامی است")]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Range(0, 1000000000, ErrorMessage = "مبلغ خرید نامعتبر است")]
        public decimal? PurchaseAmount { get; set; }
    }

    public class RedeemReferralCodeDto
    {
        [Required(ErrorMessage = "کد معرف الزامی است")]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Range(1, 1000000000, ErrorMessage = "مبلغ خرید نامعتبر است")]
        public decimal? PurchaseAmount { get; set; }

        public int? CustomerContactId { get; set; }

        public int? ReferrerContactId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    public class UpdateReferralProgramDto
    {
        [MaxLength(200, ErrorMessage = "نام برنامه حداکثر 200 کاراکتر است")]
        public string? Title { get; set; }

        public bool? IsActive { get; set; }

        public decimal? ReferrerRewardValue { get; set; }

        public bool? IsCustomerRewardActive { get; set; }

        public decimal? CustomerRewardValue { get; set; }

        public DateTime? EndDate { get; set; }
    }

    #endregion
}
