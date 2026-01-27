namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای آخرین کمپین‌ها (مطابق صفحه Messages)
    /// </summary>
    public class LatestCampaignsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime? SentAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string Status { get; set; } = string.Empty; // Sent, Scheduled
        public bool IsActive { get; set; } = true;
    }
}


