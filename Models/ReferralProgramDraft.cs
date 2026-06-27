namespace Api_Vapp.Models
{
    /// <summary>
    /// پیش‌نویس چندمرحله‌ای ایجاد برنامه پاداش
    /// </summary>
    public class ReferralProgramDraft
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string DraftId { get; set; } = string.Empty;

        public string Step1Data { get; set; } = string.Empty;

        public string? Step2Data { get; set; }

        public string? Step3Data { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
