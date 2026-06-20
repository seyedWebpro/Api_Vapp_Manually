using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class SmsApprovalRequestResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserFullName { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int? MessageCampaignId { get; set; }
        public int MessageId { get; set; }
        public int? MessageSessionId { get; set; }
        public string ContentPreview { get; set; } = string.Empty;
        public string? TitlePreview { get; set; }
        public int RecipientsCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TemplateApprovalResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserFullName { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class RejectApprovalDto
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminDashboardStatsDto
    {
        public int PendingSmsApprovals { get; set; }
        public int PendingTemplateApprovals { get; set; }
        public int OpenTickets { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSubscriptions { get; set; }
    }

    public class AdminDashboardChartPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class AdminDashboardChartsDto
    {
        public List<AdminDashboardChartPointDto> UserGrowthLast7Days { get; set; } = new();
        public List<AdminDashboardChartPointDto> MonthlyActivity { get; set; } = new();
    }
}
