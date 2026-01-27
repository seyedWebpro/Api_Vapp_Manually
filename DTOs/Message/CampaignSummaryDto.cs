namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای خلاصه کمپین (مطابق صفحه Summary and Settings)
    /// </summary>
    public class CampaignSummaryDto
    {
        // تنظیمات تکمیلی
        public CampaignSendType SendType { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool PreventDuplicate { get; set; }
        public int DuplicatePreventionHours { get; set; }
        public bool SendToSpecificTags { get; set; }
        public List<int>? SelectedTagIds { get; set; }

        // خلاصه
        public int RecipientsCount { get; set; }
        public int PartsCount { get; set; }
        public decimal CostPerPart { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public string WalletStatus { get; set; } = string.Empty; // Sufficient, Insufficient
        public decimal WalletBalance { get; set; }

        // نتایج ارسال خودکار (اگر AutoSend = true باشد)
        public bool? AutoSent { get; set; }
        public int? SentCount { get; set; }
        public int? FailedCount { get; set; }
        public decimal? ActualCost { get; set; }
    }
}


