namespace Api_Vapp.Models
{
    /// <summary>
    /// برنامه پاداش و معرف — یک کد عمومی برای هر برنامه
    /// </summary>
    public class ReferralProgram
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Percentage | FixedAmount
        /// </summary>
        public string RewardType { get; set; } = ReferralRewardTypes.Percentage;

        public decimal ReferrerRewardValue { get; set; }

        public bool IsCustomerRewardActive { get; set; }

        public decimal? CustomerRewardValue { get; set; }

        /// <summary>
        /// کد عمومی تخفیف — یکتا برای هر کاربر (صاحب کسب‌وکار)
        /// </summary>
        public string PublicCode { get; set; } = string.Empty;

        /// <summary>
        /// All | SpecificNotebooks | Individual
        /// </summary>
        public string TargetAudience { get; set; } = ReferralTargetAudience.All;

        public string? TargetNotebookIds { get; set; }

        public string? TargetContactIds { get; set; }

        public string? TargetTagIds { get; set; }

        public bool SendToSpecificTags { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int NotifiedContactsCount { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }

    public static class ReferralRewardTypes
    {
        public const string Percentage = "Percentage";
        public const string FixedAmount = "FixedAmount";
    }

    public static class ReferralTargetAudience
    {
        public const string All = "All";
        public const string SpecificNotebooks = "SpecificNotebooks";
        public const string Individual = "Individual";
    }
}
