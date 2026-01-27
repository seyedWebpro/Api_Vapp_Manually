using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Cashback
{
    #region Response DTOs

    /// <summary>
    /// خلاصه کش‌بک مخاطب
    /// اطلاعات کش‌بک یک مخاطب خاص برای نمایش در صفحه جزئیات مخاطب
    /// </summary>
    public class ContactCashbackSummaryDto
    {
        /// <summary>
        /// شناسه مخاطب
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// نام مخاطب
        /// </summary>
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// شماره موبایل مخاطب
        /// </summary>
        public string MobileNumber { get; set; } = string.Empty;

        /// <summary>
        /// کش‌بک فعلی (موجودی کل) - تومان
        /// </summary>
        public decimal TotalCashback { get; set; }

        /// <summary>
        /// کش‌بک فعلی فرمت شده
        /// </summary>
        public string FormattedTotalCashback { get; set; } = string.Empty;

        /// <summary>
        /// کش‌بک قابل استفاده - تومان
        /// </summary>
        public decimal UsableCashback { get; set; }

        /// <summary>
        /// کش‌بک قابل استفاده فرمت شده
        /// </summary>
        public string FormattedUsableCashback { get; set; } = string.Empty;

        /// <summary>
        /// روزهای باقیمانده تا انقضا
        /// </summary>
        public int? ExpiryDays { get; set; }

        /// <summary>
        /// متن نمایشی تاریخ انقضا
        /// </summary>
        public string ExpiryDaysText { get; set; } = string.Empty;

        /// <summary>
        /// درصد کش‌بک فعال
        /// </summary>
        public decimal? CashbackPercentage { get; set; }

        /// <summary>
        /// متن نمایشی درصد کش‌بک
        /// </summary>
        public string CashbackPercentageText { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ انقضا
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// آیا کش‌بک دارد؟
        /// </summary>
        public bool HasCashback { get; set; }

        /// <summary>
        /// تاریخ آخرین به‌روزرسانی
        /// </summary>
        public DateTime? LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// تاریخچه تراکنش‌های کش‌بک دستی مخاطب
    /// </summary>
    public class ManualCashbackTransactionDto
    {
        /// <summary>
        /// شناسه تراکنش
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// نوع تراکنش (Add: افزودن، Withdraw: برداشت)
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// نوع تراکنش فارسی
        /// </summary>
        public string TransactionTypePersian { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ تراکنش
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
        /// توضیحات
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// تاریخ انقضا (برای تراکنش‌های افزودن)
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// تاریخ ایجاد فرمت شده
        /// </summary>
        public string FormattedCreatedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// لیست تراکنش‌های کش‌بک دستی
    /// </summary>
    public class ManualCashbackTransactionListDto
    {
        /// <summary>
        /// لیست تراکنش‌ها
        /// </summary>
        public List<ManualCashbackTransactionDto> Transactions { get; set; } = new();

        /// <summary>
        /// تعداد کل
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// شماره صفحه
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// تعداد در هر صفحه
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// تعداد کل صفحات
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// نتیجه افزودن کش‌بک دستی
    /// </summary>
    public class AddManualCashbackResultDto
    {
        /// <summary>
        /// آیا موفق بود؟
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// شناسه تراکنش ایجاد شده
        /// </summary>
        public int TransactionId { get; set; }

        /// <summary>
        /// مبلغ افزوده شده
        /// </summary>
        public decimal AddedAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedAddedAmount { get; set; } = string.Empty;

        /// <summary>
        /// موجودی جدید
        /// </summary>
        public decimal NewBalance { get; set; }

        /// <summary>
        /// موجودی جدید فرمت شده
        /// </summary>
        public string FormattedNewBalance { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ انقضا
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
    }

    /// <summary>
    /// نتیجه برداشت کش‌بک
    /// </summary>
    public class WithdrawCashbackResultDto
    {
        /// <summary>
        /// آیا موفق بود؟
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// شناسه تراکنش ایجاد شده
        /// </summary>
        public int TransactionId { get; set; }

        /// <summary>
        /// مبلغ برداشت شده
        /// </summary>
        public decimal WithdrawnAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedWithdrawnAmount { get; set; } = string.Empty;

        /// <summary>
        /// موجودی جدید
        /// </summary>
        public decimal NewBalance { get; set; }

        /// <summary>
        /// موجودی جدید فرمت شده
        /// </summary>
        public string FormattedNewBalance { get; set; } = string.Empty;
    }

    #endregion

    #region Request DTOs

    /// <summary>
    /// درخواست افزودن کش‌بک دستی به مخاطب
    /// </summary>
    public class AddManualCashbackDto
    {
        /// <summary>
        /// شناسه مخاطب
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        /// <summary>
        /// مبلغ کش‌بک (تومان)
        /// </summary>
        [Required(ErrorMessage = "مبلغ کش‌بک الزامی است")]
        [Range(1000, 100000000, ErrorMessage = "مبلغ کش‌بک باید بین 1,000 تا 100,000,000 تومان باشد")]
        public decimal Amount { get; set; }

        /// <summary>
        /// توضیحات
        /// </summary>
        [MaxLength(500, ErrorMessage = "توضیحات نباید بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>
        /// روزهای اعتبار (پیش‌فرض: 30 روز)
        /// </summary>
        [Range(1, 365, ErrorMessage = "روزهای اعتبار باید بین 1 تا 365 روز باشد")]
        public int ValidityDays { get; set; } = 30;
    }

    /// <summary>
    /// درخواست برداشت کش‌بک از مخاطب
    /// </summary>
    public class WithdrawCashbackDto
    {
        /// <summary>
        /// شناسه مخاطب
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        /// <summary>
        /// مبلغ برداشت (تومان)
        /// </summary>
        [Required(ErrorMessage = "مبلغ برداشت الزامی است")]
        [Range(1, 100000000, ErrorMessage = "مبلغ برداشت باید بین 1 تا 100,000,000 تومان باشد")]
        public decimal Amount { get; set; }

        /// <summary>
        /// دلیل برداشت (اختیاری)
        /// </summary>
        [MaxLength(500, ErrorMessage = "دلیل برداشت نباید بیشتر از 500 کاراکتر باشد")]
        public string? Reason { get; set; }
    }

    #endregion
}
