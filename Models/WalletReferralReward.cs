namespace Api_Vapp.Models
{
    /// <summary>
    /// دفتر ثبت پاداش معرفی — هر پرداخت موفق حداکثر یک پاداش (idempotent).
    /// </summary>
    public class WalletReferralReward
    {
        public int Id { get; set; }

        /// <summary>پرداخت مرتبط (یکتا)</summary>
        public int PaymentId { get; set; }

        /// <summary>کاربری که شارژ کرده و از کد استفاده کرده</summary>
        public int BeneficiaryUserId { get; set; }

        /// <summary>صاحب کد معرفی (دریافت‌کننده پاداش)</summary>
        public int ReferrerUserId { get; set; }

        /// <summary>کد معرفی استفاده‌شده</summary>
        public string ReferralCode { get; set; } = string.Empty;

        /// <summary>مبلغ درخواستی شارژ کیف پول (اعتبار واریزی به ذینفع)</summary>
        public decimal RequestedAmount { get; set; }

        /// <summary>مبلغ پرداخت‌شده در درگاه</summary>
        public decimal PayableAmount { get; set; }

        /// <summary>مبلغ تخفیف</summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>درصد تخفیف قفل‌شده در لحظه شارژ</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>مبلغ پاداش واریزی به معرف</summary>
        public decimal BonusAmount { get; set; }

        /// <summary>درصد پاداش قفل‌شده در لحظه شارژ</summary>
        public decimal BonusPercent { get; set; }

        /// <summary>تراکنش کیف پول پاداش معرف</summary>
        public int? ReferrerWalletTransactionId { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual Payment Payment { get; set; } = null!;
        public virtual User BeneficiaryUser { get; set; } = null!;
        public virtual User ReferrerUser { get; set; } = null!;
        public virtual WalletTransaction? ReferrerWalletTransaction { get; set; }
    }
}
