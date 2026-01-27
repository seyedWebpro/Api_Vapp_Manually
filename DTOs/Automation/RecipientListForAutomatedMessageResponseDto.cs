namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای پاسخ انتخاب گیرندگان پیام خودکار
    /// </summary>
    public class RecipientListForAutomatedMessageResponseDto
    {
        public List<RecipientItemForAutomatedMessageDto> Recipients { get; set; } = new List<RecipientItemForAutomatedMessageDto>();
        public int TotalCount { get; set; }
        public int EligibleCount { get; set; }
        public int IneligibleCount { get; set; }
        public EligibilityInfoDto EligibilityInfo { get; set; } = new EligibilityInfoDto();
        public int? SessionId { get; set; }
    }

    /// <summary>
    /// DTO برای هر گیرنده در پیام خودکار
    /// </summary>
    public class RecipientItemForAutomatedMessageDto
    {
        public int? ContactId { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool? HasDateOfBirth { get; set; }  // فقط برای نوع Birthday
        public bool? HasCashback { get; set; }     // فقط برای نوع CashbackExpiry
        public bool IsEligible { get; set; }       // آیا واجد شرایط است یا نه
    }

    /// <summary>
    /// DTO برای اطلاعات واجد شرایط بودن
    /// </summary>
    public class EligibilityInfoDto
    {
        public string Message { get; set; } = string.Empty;
        public string? Warning { get; set; }
    }
}

