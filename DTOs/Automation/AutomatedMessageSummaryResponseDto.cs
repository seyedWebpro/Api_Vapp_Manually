namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای پاسخ محاسبه خلاصه پیام خودکار
    /// </summary>
    public class AutomatedMessageSummaryResponseDto
    {
        public string AutomationType { get; set; } = string.Empty;
        public string ExecutionTime { get; set; } = string.Empty;
        public int RecipientsCount { get; set; }
        public int EligibleRecipientsCount { get; set; }
        public int IneligibleRecipientsCount { get; set; }
        public EligibilityInfoDto EligibilityInfo { get; set; } = new EligibilityInfoDto();
        public decimal CostPerPart { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public string WalletStatus { get; set; } = string.Empty; // Sufficient, Insufficient
        public decimal WalletBalance { get; set; }
        public bool PreventDuplicate { get; set; }
        public int DuplicatePreventionHours { get; set; }
        public bool SendToSpecificTags { get; set; }
        public List<int>? SelectedTagIds { get; set; }
    }
}

