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
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// پس از تأیید، ارسال با این قالب تا ویرایش بعدی بدون صف تأیید پیام انجام می‌شود.
        /// </summary>
        public bool SkipsMessageApprovalQueue { get; set; }
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

        /// <summary>تعداد پیامک‌های موفق امروز (UTC)</summary>
        public int SmsSentToday { get; set; }

        /// <summary>تعداد پیامک‌های موفق ۷ روز گذشته (UTC)</summary>
        public int SmsSentThisWeek { get; set; }

        /// <summary>تعداد پیامک‌های موفق ماه جاری (UTC)</summary>
        public int SmsSentThisMonth { get; set; }

        /// <summary>مجموع صفحات پیامک ارسال‌شده امروز (UTC) — بر اساس متن واقعی یا PartsCount</summary>
        public int SmsPagesToday { get; set; }

        /// <summary>مجموع صفحات پیامک ارسال‌شده ۷ روز گذشته (UTC)</summary>
        public int SmsPagesThisWeek { get; set; }

        /// <summary>مجموع صفحات پیامک ارسال‌شده ماه جاری (UTC)</summary>
        public int SmsPagesThisMonth { get; set; }

        /// <summary>تعرفه فعلی هر صفحه پیامک (تومان)</summary>
        public decimal CostPerPart { get; set; }

        /// <summary>هزینه تخمینی امروز بر اساس صفحات × تعرفه فعلی</summary>
        public decimal SmsEstimatedCostToday { get; set; }

        /// <summary>هزینه تخمینی ۷ روز گذشته</summary>
        public decimal SmsEstimatedCostThisWeek { get; set; }

        /// <summary>هزینه تخمینی ماه جاری</summary>
        public decimal SmsEstimatedCostThisMonth { get; set; }

        /// <summary>مبلغ واقعی کسرشده از کیف پول بابت پیامک — امروز</summary>
        public decimal SmsChargedCostToday { get; set; }

        /// <summary>مبلغ واقعی کسرشده از کیف پول بابت پیامک — ۷ روز گذشته</summary>
        public decimal SmsChargedCostThisWeek { get; set; }

        /// <summary>مبلغ واقعی کسرشده از کیف پول بابت پیامک — ماه جاری</summary>
        public decimal SmsChargedCostThisMonth { get; set; }

        /// <summary>آیا صورتحساب پیامک در تعرفه فعال است؟</summary>
        public bool IsSmsBillingEnabled { get; set; }
    }

    public class AdminDashboardChartPointDto
    {
        public string Label { get; set; } = string.Empty;

        /// <summary>مقدار اصلی نمودار (صفحات پیامک یا تعداد رویداد)</summary>
        public int Value { get; set; }

        /// <summary>هزینه تخمینی مرتبط با Value (تومان) — فقط برای سری صفحات پیامک</summary>
        public decimal? EstimatedCost { get; set; }

        /// <summary>هزینه واقعی کسرشده از کیف پول در همان بازه (تومان)</summary>
        public decimal? ChargedCost { get; set; }
    }

    public class AdminDashboardChartsDto
    {
        public List<AdminDashboardChartPointDto> UserGrowthLast7Days { get; set; } = new();
        public List<AdminDashboardChartPointDto> MonthlyActivity { get; set; } = new();

        /// <summary>صفحات پیامک — ۷ روز گذشته (روزانه)</summary>
        public List<AdminDashboardChartPointDto> SmsPagesDaily { get; set; } = new();

        /// <summary>صفحات پیامک — ۸ هفته گذشته (هفتگی، شروع از شنبه)</summary>
        public List<AdminDashboardChartPointDto> SmsPagesWeekly { get; set; } = new();

        /// <summary>صفحات پیامک — ۱۲ ماه گذشته (ماهانه)</summary>
        public List<AdminDashboardChartPointDto> SmsPagesMonthly { get; set; } = new();
    }
}
