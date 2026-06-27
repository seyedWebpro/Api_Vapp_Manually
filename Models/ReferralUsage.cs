namespace Api_Vapp.Models
{
    /// <summary>
    /// ثبت هر بار مصرف کد عمومی برنامه پاداش در فروشگاه
    /// </summary>
    public class ReferralUsage
    {
        public int Id { get; set; }

        public int ReferralProgramId { get; set; }

        public int UserId { get; set; }

        public string PublicCode { get; set; } = string.Empty;

        public decimal? PurchaseAmount { get; set; }

        public decimal CustomerDiscountAmount { get; set; }

        public decimal ReferrerRewardAmount { get; set; }

        public int? CustomerContactId { get; set; }

        public int? ReferrerContactId { get; set; }

        public string Status { get; set; } = ReferralUsageStatuses.Completed;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ReferralProgram ReferralProgram { get; set; } = null!;

        public virtual User User { get; set; } = null!;

        public virtual Contact? CustomerContact { get; set; }

        public virtual Contact? ReferrerContact { get; set; }
    }

    public static class ReferralUsageStatuses
    {
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }
}
