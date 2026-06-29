namespace Api_Vapp.Models
{
    /// <summary>
    /// اشتراک فعال کاربر
    /// </summary>
    public class UserSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = "Active";
        public int? SourcePaymentId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual SubscriptionPlan Plan { get; set; } = null!;
        public virtual Payment? SourcePayment { get; set; }
    }
}
