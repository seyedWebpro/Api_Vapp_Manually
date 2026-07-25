namespace Api_Vapp.Models
{
    /// <summary>
    /// جلسه دسترسی عمومی شرکت‌کننده — قبل از چرخش گردونه / ارسال فرم
    /// </summary>
    public class PublicParticipantSession
    {
        public int Id { get; set; }

        public PublicParticipantResourceType ResourceType { get; set; }

        /// <summary>
        /// شناسه گردونه یا فرم
        /// </summary>
        public int ResourceId { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        /// <summary>
        /// هش SHA-256 توکن دسترسی (توکن خام فقط یک‌بار به کلاینت داده می‌شود)
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// زمان تأیید شماره موبایل با OTP — برای فرم عمومی الزامی است
        /// </summary>
        public DateTime? PhoneVerifiedAt { get; set; }

        /// <summary>
        /// زمان مصرف جلسه (چرخش / ارسال فرم)
        /// </summary>
        public DateTime? ConsumedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
