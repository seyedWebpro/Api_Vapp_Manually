namespace Api_Vapp.Models
{
    public class SubscriptionDiscountUsage
    {
        public int Id { get; set; }
        public int SubscriptionDiscountCodeId { get; set; }
        public int UserId { get; set; }
        public int? PaymentId { get; set; }
        public int? UserSubscriptionId { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        public virtual SubscriptionDiscountCode DiscountCode { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual Payment? Payment { get; set; }
        public virtual UserSubscription? UserSubscription { get; set; }
    }
}
