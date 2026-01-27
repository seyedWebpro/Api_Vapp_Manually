using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Payment
{
    #region Response DTOs

    /// <summary>
    /// اطلاعات پرداخت
    /// </summary>
    public class PaymentDto
    {
        public int Id { get; set; }

        /// <summary>
        /// مبلغ پرداخت (تومان)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedAmount { get; set; } = string.Empty;

        /// <summary>
        /// نوع پرداخت
        /// </summary>
        public string PaymentType { get; set; } = string.Empty;

        /// <summary>
        /// عنوان نوع پرداخت
        /// </summary>
        public string PaymentTypeTitle { get; set; } = string.Empty;

        /// <summary>
        /// درگاه پرداخت
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// شماره سفارش
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// شماره مرجع درگاه
        /// </summary>
        public string? RefId { get; set; }

        /// <summary>
        /// شماره پیگیری بانکی
        /// </summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// شماره تراکنش بانکی
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// شماره کارت پرداخت کننده (Masked)
        /// </summary>
        public string? CardNumber { get; set; }

        /// <summary>
        /// وضعیت پرداخت
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// عنوان وضعیت
        /// </summary>
        public string StatusTitle { get; set; } = string.Empty;

        /// <summary>
        /// پیام خطا (در صورت وجود)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// تاریخ شمسی ایجاد
        /// </summary>
        public string PersianCreatedAt { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ پرداخت
        /// </summary>
        public DateTime? PaidAt { get; set; }

        /// <summary>
        /// تاریخ تأیید
        /// </summary>
        public DateTime? VerifiedAt { get; set; }
    }

    /// <summary>
    /// نتیجه پرداخت
    /// </summary>
    public class PaymentResultDto
    {
        /// <summary>
        /// آیا پرداخت موفق بود؟
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// پیام
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// اطلاعات پرداخت
        /// </summary>
        public PaymentDto? Payment { get; set; }

        /// <summary>
        /// موجودی جدید کیف پول (در صورت موفقیت)
        /// </summary>
        public decimal? NewBalance { get; set; }

        /// <summary>
        /// موجودی فرمت شده
        /// </summary>
        public string? FormattedNewBalance { get; set; }
    }

    /// <summary>
    /// لیست پرداخت‌ها
    /// </summary>
    public class PaymentListDto
    {
        public List<PaymentDto> Payments { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// اطلاعات درگاه پرداخت
    /// </summary>
    public class PaymentGatewayInfoDto
    {
        /// <summary>
        /// کد درگاه
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// نام درگاه
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// URL لوگو
        /// </summary>
        public string? LogoUrl { get; set; }

        /// <summary>
        /// آیا فعال است؟
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// آیا به زودی خواهد آمد؟
        /// </summary>
        public bool ComingSoon { get; set; }
    }

    #endregion

    #region Request DTOs

    /// <summary>
    /// درخواست تأیید پرداخت (Callback از درگاه)
    /// </summary>
    public class VerifyPaymentRequestDto
    {
        /// <summary>
        /// شناسه پرداخت
        /// </summary>
        [Required(ErrorMessage = "شناسه پرداخت الزامی است")]
        public int PaymentId { get; set; }

        /// <summary>
        /// شماره سفارش
        /// </summary>
        public string? OrderId { get; set; }

        /// <summary>
        /// شماره مرجع درگاه (RefId)
        /// </summary>
        public string? RefId { get; set; }

        /// <summary>
        /// شماره تراکنش درگاه
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// کد وضعیت درگاه
        /// </summary>
        public string? ResCode { get; set; }

        /// <summary>
        /// شماره کارت (Masked)
        /// </summary>
        public string? CardNumber { get; set; }

        /// <summary>
        /// شماره پیگیری بانکی
        /// </summary>
        public string? SaleReferenceId { get; set; }
    }

    /// <summary>
    /// درخواست ایجاد پرداخت (برای استفاده داخلی)
    /// </summary>
    public class CreatePaymentDto
    {
        /// <summary>
        /// مبلغ پرداخت (تومان)
        /// </summary>
        [Required(ErrorMessage = "مبلغ پرداخت الزامی است")]
        [Range(10000, 100000000, ErrorMessage = "مبلغ پرداخت باید بین 10,000 تا 100,000,000 تومان باشد")]
        public decimal Amount { get; set; }

        /// <summary>
        /// نوع پرداخت
        /// </summary>
        [Required(ErrorMessage = "نوع پرداخت الزامی است")]
        public string PaymentType { get; set; } = string.Empty;

        /// <summary>
        /// درگاه پرداخت
        /// </summary>
        [Required(ErrorMessage = "درگاه پرداخت الزامی است")]
        public string Gateway { get; set; } = "Behpardakht";

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// URL بازگشت بعد از پرداخت
        /// </summary>
        public string? CallbackUrl { get; set; }
    }

    #endregion

    #region Gateway Specific DTOs

    /// <summary>
    /// پاسخ درگاه به‌پرداخت - درخواست توکن
    /// </summary>
    public class BehpardakhtTokenResponse
    {
        public int ResCode { get; set; }
        public string? RefId { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// پاسخ درگاه به‌پرداخت - تأیید پرداخت
    /// </summary>
    public class BehpardakhtVerifyResponse
    {
        public int ResCode { get; set; }
        public string? Message { get; set; }
        public string? SaleReferenceId { get; set; }
        public string? CardNumber { get; set; }
    }

    /// <summary>
    /// پاسخ درگاه به‌پرداخت - تسویه پرداخت
    /// </summary>
    public class BehpardakhtSettleResponse
    {
        public int ResCode { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}




