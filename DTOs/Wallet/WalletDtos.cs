using System.ComponentModel.DataAnnotations;
using Api_Vapp.DTOs.Cashback;

namespace Api_Vapp.DTOs.Wallet
{
    #region Response DTOs

    /// <summary>
    /// اطلاعات کیف پول کاربر
    /// </summary>
    public class WalletInfoDto
    {
        /// <summary>
        /// موجودی کیف پول (تومان)
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// موجودی قابل نمایش (فرمت شده)
        /// </summary>
        public string FormattedBalance { get; set; } = string.Empty;

        /// <summary>
        /// تعداد کش‌بک‌های فعال
        /// </summary>
        public int ActiveCashbacksCount { get; set; }

        /// <summary>
        /// تعداد تراکنش‌ها
        /// </summary>
        public int TotalTransactionsCount { get; set; }

        /// <summary>
        /// آخرین به‌روزرسانی
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// تراکنش کیف پول
    /// </summary>
    public class WalletTransactionDto
    {
        public int Id { get; set; }

        /// <summary>
        /// نوع تراکنش
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// عنوان تراکنش
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// مبلغ تراکنش (مثبت برای واریز، منفی برای برداشت)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedAmount { get; set; } = string.Empty;

        /// <summary>
        /// موجودی قبل از تراکنش
        /// </summary>
        public decimal BalanceBefore { get; set; }

        /// <summary>
        /// موجودی بعد از تراکنش
        /// </summary>
        public decimal BalanceAfter { get; set; }

        /// <summary>
        /// شماره پیگیری
        /// </summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// وضعیت تراکنش
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// تاریخ شمسی ایجاد
        /// </summary>
        public string PersianCreatedAt { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ تکمیل
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// لیست تراکنش‌های کیف پول
    /// </summary>
    public class WalletTransactionListDto
    {
        public List<WalletTransactionDto> Transactions { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// اطلاعات کامل صفحه کیف پول
    /// شامل موجودی، کش‌بک‌های فعال و تاریخچه مالی
    /// </summary>
    public class WalletPageDto
    {
        /// <summary>
        /// موجودی کیف پول (تومان)
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// موجودی فرمت شده
        /// </summary>
        public string FormattedBalance { get; set; } = string.Empty;

        /// <summary>
        /// لیست کش‌بک‌های فعال
        /// </summary>
        public List<CashbackDto> ActiveCashbacks { get; set; } = new();

        /// <summary>
        /// تاریخچه مالی (آخرین تراکنش‌ها)
        /// </summary>
        public List<WalletTransactionDto> RecentTransactions { get; set; } = new();

        /// <summary>
        /// تعداد کل تراکنش‌ها
        /// </summary>
        public int TotalTransactionsCount { get; set; }
    }

    #endregion

    #region Request DTOs

    /// <summary>
    /// درخواست شارژ کیف پول
    /// </summary>
    public class ChargeWalletRequestDto
    {
        /// <summary>
        /// مبلغ شارژ (تومان) - حداقل 10,000 تومان
        /// </summary>
        [Required(ErrorMessage = "مبلغ شارژ الزامی است")]
        [Range(10000, 100000000, ErrorMessage = "مبلغ شارژ باید بین 10,000 تا 100,000,000 تومان باشد")]
        public decimal Amount { get; set; }

        /// <summary>
        /// درگاه پرداخت
        /// </summary>
        [Required(ErrorMessage = "درگاه پرداخت الزامی است")]
        public string Gateway { get; set; } = "Behpardakht";

        /// <summary>
        /// URL بازگشت بعد از پرداخت
        /// </summary>
        public string? CallbackUrl { get; set; }
    }

    /// <summary>
    /// پاسخ شارژ کیف پول
    /// </summary>
    public class ChargeWalletResponseDto
    {
        /// <summary>
        /// شناسه پرداخت
        /// </summary>
        public int PaymentId { get; set; }

        /// <summary>
        /// شماره سفارش
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ پرداخت (تومان)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// URL انتقال به درگاه پرداخت
        /// </summary>
        public string GatewayUrl { get; set; } = string.Empty;

        /// <summary>
        /// نوع درگاه
        /// </summary>
        public string Gateway { get; set; } = string.Empty;
    }

    #endregion
}




