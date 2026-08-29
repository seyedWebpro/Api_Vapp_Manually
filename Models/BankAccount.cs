namespace Api_Vapp.Models
{
    /// <summary>
    /// شماره حساب بانکی کاربر برای ارسال سریع (حساب / کارت / شبا)
    /// </summary>
    public class BankAccount : IQuickSendApprovable
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>عنوان نمایشی (مثلاً نام بانک یا برچسب حساب)</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات اختیاری ارسال SMS (قبل از عنوان و شماره‌ها) — حداکثر ۱۰۰ کاراکتر.
        /// در API به‌صورت <c>smsDescription</c> expose می‌شود.
        /// </summary>
        public string? SmsCaption { get; set; }

        /// <summary>شماره حساب بانکی</summary>
        public string? AccountNumber { get; set; }

        /// <summary>شماره کارت ۱۶ رقمی</summary>
        public string? CardNumber { get; set; }

        /// <summary>شماره شبا (IR + ۲۴ رقم)</summary>
        public string? ShebaNumber { get; set; }

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        #region Quick-send approval

        /// <summary>وضعیت تأیید ادمین برای ارسال سریع (Pending / Approved / Rejected)</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        public string? RejectionReason { get; set; }

        #endregion

        public virtual User User { get; set; } = null!;
    }
}
