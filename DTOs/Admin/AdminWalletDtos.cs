using System.ComponentModel.DataAnnotations;
using Api_Vapp.DTOs.Wallet;

namespace Api_Vapp.DTOs.Admin
{
    /// <summary>
    /// موجودی کیف پول کاربر برای پنل ادمین
    /// </summary>
    public class AdminWalletBalanceDto
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Balance { get; set; }
        public string FormattedBalance { get; set; } = string.Empty;
        public int TotalTransactionsCount { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// درخواست شارژ دستی کیف پول توسط ادمین
    /// </summary>
    public class AdminManualChargeRequestDto
    {
        /// <summary>
        /// مبلغ شارژ (تومان) — فقط عدد صحیح
        /// </summary>
        [Required(ErrorMessage = "مبلغ شارژ الزامی است")]
        [Range(1, 100_000_000, ErrorMessage = "مبلغ شارژ باید بین ۱ تا ۱۰۰٬۰۰۰٬۰۰۰ تومان باشد")]
        public decimal Amount { get; set; }

        /// <summary>
        /// دلیل/توضیح حسابداری (الزامی برای ردیابی مالی)
        /// </summary>
        [Required(ErrorMessage = "دلیل شارژ الزامی است")]
        [MinLength(5, ErrorMessage = "دلیل شارژ باید حداقل ۵ کاراکتر باشد")]
        [MaxLength(500, ErrorMessage = "دلیل شارژ نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// کلید یکتای کلاینت برای جلوگیری از ثبت تکراری (دوبار کلیک / retry)
        /// </summary>
        [MaxLength(64, ErrorMessage = "کلید یکتایی نمی‌تواند بیشتر از ۶۴ کاراکتر باشد")]
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// نتیجه شارژ دستی ادمین
    /// </summary>
    public class AdminManualChargeResponseDto
    {
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string FormattedAmount { get; set; } = string.Empty;
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string FormattedBalanceAfter { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public bool WasAlreadyProcessed { get; set; }
        public WalletTransactionDto Transaction { get; set; } = null!;
    }
}
