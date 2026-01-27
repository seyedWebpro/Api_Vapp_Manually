namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای گزارش جامع شامل آمار کمپین‌ها، عملکرد کارگران، و بررسی داده‌ها
    /// </summary>
    public class ComprehensiveReportDto
    {
        /// <summary>
        /// آمار کلی پیام‌ها
        /// </summary>
        public MessageStatisticsDto MessageStatistics { get; set; } = new MessageStatisticsDto();

        /// <summary>
        /// آمار کمپین‌ها
        /// </summary>
        public CampaignStatisticsDto CampaignStatistics { get; set; } = new CampaignStatisticsDto();

        /// <summary>
        /// عملکرد کارگران (workers)
        /// </summary>
        public List<WorkerPerformanceDto> WorkerPerformances { get; set; } = new List<WorkerPerformanceDto>();

        /// <summary>
        /// آمار پیام‌های خودکار
        /// </summary>
        public AutomatedMessageStatisticsDto AutomatedMessageStatistics { get; set; } = new AutomatedMessageStatisticsDto();

        /// <summary>
        /// بررسی صحت داده‌ها
        /// </summary>
        public DataValidationDto DataValidation { get; set; } = new DataValidationDto();
    }

    /// <summary>
    /// آمار کلی پیام‌ها
    /// </summary>
    public class MessageStatisticsDto
    {
        public int TotalMessages { get; set; }
        public int SentToday { get; set; }
        public int SentThisWeek { get; set; }
        public int SentThisMonth { get; set; }
        public int ScheduledCount { get; set; }
        public int FailedCount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerMessage { get; set; }
    }

    /// <summary>
    /// آمار کمپین‌ها
    /// </summary>
    public class CampaignStatisticsDto
    {
        public int TotalCampaigns { get; set; }
        public int ActiveCampaigns { get; set; }
        public int CompletedCampaigns { get; set; }
        public int ScheduledCampaigns { get; set; }
        public int FailedCampaigns { get; set; }
        public DateTime? LastCampaignSentAt { get; set; }
        public int TotalRecipientsInCampaigns { get; set; }
        public int TotalMessagesInCampaigns { get; set; }
    }

    /// <summary>
    /// عملکرد کارگر (worker)
    /// </summary>
    public class WorkerPerformanceDto
    {
        public int WorkerId { get; set; }
        public string WorkerName { get; set; } = string.Empty;
        public int MessagesSent { get; set; }
        public int CampaignsCreated { get; set; }
        public int RecipientsReached { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerMessage { get; set; }
        public DateTime? LastActivity { get; set; }
        public int SuccessRate { get; set; } // درصد موفقیت
        public int PointsEarned { get; set; } // امتیازات کسب شده
    }

    /// <summary>
    /// آمار پیام‌های خودکار
    /// </summary>
    public class AutomatedMessageStatisticsDto
    {
        public int TotalAutomatedMessages { get; set; }
        public int ActiveAutomatedMessages { get; set; }
        public int CampaignsCreatedFromAutomation { get; set; }
        public int MessagesSentByAutomation { get; set; }
        public Dictionary<string, int> AutomationTypeCounts { get; set; } = new Dictionary<string, int>();
        public DateTime? LastAutomatedCampaignCreated { get; set; }
        public int TotalAutomationRecipients { get; set; }
    }

    /// <summary>
    /// بررسی صحت داده‌ها
    /// </summary>
    public class DataValidationDto
    {
        public bool IsDataValid { get; set; }
        public List<string> ValidationIssues { get; set; } = new List<string>();
        public int OrphanedMessages { get; set; } // پیام‌هایی بدون کمپین
        public int OrphanedRecipients { get; set; } // گیرندگانی بدون کمپین
        public int InvalidContacts { get; set; } // مخاطبینی با شماره نامعتبر
        public int MissingWalletBalances { get; set; } // کیف پول‌های نامعتبر
        public int DuplicateMessages { get; set; } // پیام‌های تکراری
        public int DataIntegrityScore { get; set; } // امتیاز صحت داده‌ها (0-100)
    }
}
