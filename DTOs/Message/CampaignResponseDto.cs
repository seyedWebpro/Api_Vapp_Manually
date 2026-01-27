namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پاسخ کمپین
    /// </summary>
    public class CampaignResponseDto
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string? Title { get; set; }
        public CampaignSendType SendType { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool PreventDuplicate { get; set; }
        public int DuplicatePreventionHours { get; set; }
        public bool SendToSpecificTags { get; set; }
        public List<int>? SelectedTagIds { get; set; }
        public int RecipientsCount { get; set; }
        public int PartsCount { get; set; }
        public decimal CostPerPart { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public decimal ActualTotalCost { get; set; }
        public string WalletStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? SentAt { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}


