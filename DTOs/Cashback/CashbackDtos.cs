using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Cashback
{
    #region Response DTOs

    /// <summary>
    /// اطلاعات کش‌بک
    /// </summary>
    public class CashbackDto
    {
        public int Id { get; set; }

        /// <summary>
        /// عنوان کش‌بک
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// نوع کش‌بک (Percentage: درصدی، FixedAmount: مبلغ ثابت)
        /// </summary>
        public string CashbackType { get; set; } = string.Empty;

        /// <summary>
        /// درصد کش‌بک (برای نوع درصدی)
        /// </summary>
        public decimal? Percentage { get; set; }

        /// <summary>
        /// مبلغ ثابت کش‌بک (برای نوع مبلغ ثابت) - تومان
        /// </summary>
        public decimal? FixedAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedAmount { get; set; } = string.Empty;

        /// <summary>
        /// حداکثر مبلغ کش‌بک برای هر خرید
        /// </summary>
        public decimal? MaxCashbackAmount { get; set; }

        /// <summary>
        /// حداقل مبلغ خرید برای دریافت کش‌بک
        /// </summary>
        public decimal? MinPurchaseAmount { get; set; }

        /// <summary>
        /// مدت اعتبار به روز
        /// </summary>
        public int ValidityDays { get; set; }

        /// <summary>
        /// روزهای باقیمانده تا انقضا
        /// </summary>
        public int? RemainingDays { get; set; }

        /// <summary>
        /// تاریخ شروع اعتبار
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// تاریخ پایان اعتبار
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// زمان واریز کش‌بک
        /// </summary>
        public string DepositTiming { get; set; } = string.Empty;

        /// <summary>
        /// زمان مشخص واریز (در صورت انتخاب زمان‌بندی)
        /// </summary>
        public TimeSpan? ScheduledDepositTime { get; set; }

        /// <summary>
        /// تاریخ و زمان دقیق واریز زمان‌بندی شده (UTC)
        /// </summary>
        public DateTime? ScheduledDepositDateTime { get; set; }

        /// <summary>
        /// وضعیت پردازش زمان‌بندی شده
        /// </summary>
        public string ScheduleStatus { get; set; } = "None";

        /// <summary>
        /// توضیح وضعیت زمان‌بندی
        /// </summary>
        public string? ScheduleStatusDescription { get; set; }

        /// <summary>
        /// نوع مخاطبین
        /// </summary>
        public string TargetAudience { get; set; } = string.Empty;

        /// <summary>
        /// توضیح نوع مخاطبین
        /// </summary>
        public string TargetAudienceDescription { get; set; } = string.Empty;

        /// <summary>
        /// لیست شناسه دفترچه‌ها
        /// </summary>
        public List<int>? TargetNotebookIds { get; set; }

        /// <summary>
        /// لیست شناسه تگ‌ها
        /// </summary>
        public List<int>? TargetTagIds { get; set; }

        /// <summary>
        /// ارسال برای تگ‌های خاص
        /// </summary>
        public bool SendToSpecificTags { get; set; }

        /// <summary>
        /// وضعیت فعال بودن
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// تاریخ آخرین به‌روزرسانی
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// لیست کش‌بک‌ها
    /// </summary>
    public class CashbackListDto
    {
        public List<CashbackDto> Cashbacks { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// تراکنش کش‌بک
    /// </summary>
    public class CashbackTransactionDto
    {
        public int Id { get; set; }

        /// <summary>
        /// شناسه کش‌بک
        /// </summary>
        public int CashbackId { get; set; }

        /// <summary>
        /// عنوان کش‌بک
        /// </summary>
        public string CashbackTitle { get; set; } = string.Empty;

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
        public string ContactMobile { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ کش‌بک
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedAmount { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ خرید مرتبط
        /// </summary>
        public decimal? PurchaseAmount { get; set; }

        /// <summary>
        /// وضعیت
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// زمان واریز
        /// </summary>
        public DateTime? DepositedAt { get; set; }

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// خلاصه هزینه کش‌بک
    /// </summary>
    public class CashbackCostSummaryDto
    {
        /// <summary>
        /// نوع اتوماسیون
        /// </summary>
        public string AutomationType { get; set; } = string.Empty;

        /// <summary>
        /// زمان اجرا
        /// </summary>
        public string ExecutionTime { get; set; } = string.Empty;

        /// <summary>
        /// تعداد مخاطبین
        /// </summary>
        public int ContactsCount { get; set; }

        /// <summary>
        /// هزینه هر پارت (تومان)
        /// </summary>
        public decimal CostPerPart { get; set; }

        /// <summary>
        /// هزینه کل تخمینی (تومان)
        /// </summary>
        public decimal EstimatedTotalCost { get; set; }

        /// <summary>
        /// هزینه فرمت شده
        /// </summary>
        public string FormattedEstimatedCost { get; set; } = string.Empty;

        /// <summary>
        /// وضعیت کیف پول (کافی/ناکافی)
        /// </summary>
        public string WalletStatus { get; set; } = string.Empty;

        /// <summary>
        /// آیا موجودی کافی است؟
        /// </summary>
        public bool HasSufficientBalance { get; set; }
    }

    #endregion

    #region Request DTOs

    /// <summary>
    /// DTO برای مرحله 1 ایجاد کش‌بک (انتخاب نوع و تنظیمات اولیه)
    /// </summary>
    public class CashbackStep1Dto
    {
        /// <summary>
        /// نوع کش‌بک (Percentage: درصدی، FixedAmount: مبلغ ثابت)
        /// </summary>
        [Required(ErrorMessage = "نوع کش‌بک الزامی است")]
        public string CashbackType { get; set; } = "Percentage";

        /// <summary>
        /// درصد کش‌بک (برای نوع درصدی) - بین 1 تا 50
        /// </summary>
        [Range(1, 50, ErrorMessage = "درصد کش‌بک باید بین 1 تا 50 باشد")]
        public decimal? Percentage { get; set; }

        /// <summary>
        /// مبلغ ثابت کش‌بک (برای نوع مبلغ ثابت) - تومان
        /// </summary>
        [Range(1000, 10000000, ErrorMessage = "مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد")]
        public decimal? FixedAmount { get; set; }

        /// <summary>
        /// مبلغ کل خرید (برای محاسبه کش‌بک درصدی) - تومان
        /// این فیلد برای نمایش در UI استفاده می‌شود و در محاسبه نهایی استفاده می‌شود
        /// </summary>
        [Range(0, 100000000, ErrorMessage = "مبلغ کل خرید نامعتبر است")]
        public decimal? TotalPurchaseAmount { get; set; }

        /// <summary>
        /// مدت اعتبار به روز (1 تا 365 روز)
        /// </summary>
        [Range(1, 365, ErrorMessage = "مدت اعتبار باید بین 1 تا 365 روز باشد")]
        public int ValidityDays { get; set; } = 30;
    }

    /// <summary>
    /// پاسخ اعتبارسنجی مرحله 1
    /// </summary>
    public class CashbackStep1ValidationResponseDto
    {
        /// <summary>
        /// آیا اعتبارسنجی موفق بود؟
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// پیام‌های خطا (در صورت وجود)
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// مبلغ کش‌بک محاسبه شده (برای نوع درصدی)
        /// </summary>
        public decimal? CalculatedCashbackAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده کش‌بک
        /// </summary>
        public string? FormattedCashbackAmount { get; set; }

        /// <summary>
        /// شناسه Draft (در صورت موفق بودن اعتبارسنجی)
        /// </summary>
        public string? DraftId { get; set; }

        /// <summary>
        /// تاریخ انقضای Draft
        /// </summary>
        public DateTime? DraftExpiresAt { get; set; }
    }

    /// <summary>
    /// DTO برای مرحله 2 ایجاد کش‌بک (انتخاب مخاطبین)
    /// </summary>
    public class CashbackStep2Dto
    {
        /// <summary>
        /// شناسه Draft (برای به‌روزرسانی draft موجود)
        /// </summary>
        public string? DraftId { get; set; }

        /// <summary>
        /// نوع مخاطبین (All: همه، NewContacts: مخاطبین جدید، SpecificNotebooks: دفترچه خاص)
        /// </summary>
        [Required(ErrorMessage = "نوع مخاطبین الزامی است")]
        public string TargetAudience { get; set; } = "All";

        /// <summary>
        /// لیست شناسه دفترچه‌ها (برای نوع دفترچه خاص)
        /// </summary>
        public List<int>? TargetNotebookIds { get; set; }
    }

    /// <summary>
    /// پاسخ اعتبارسنجی مرحله 2
    /// </summary>
    public class CashbackStep2ValidationResponseDto
    {
        /// <summary>
        /// آیا اعتبارسنجی موفق بود؟
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// پیام‌های خطا (در صورت وجود)
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// تعداد کل مخاطبین
        /// </summary>
        public int TotalContactsCount { get; set; }

        /// <summary>
        /// توضیح نوع مخاطبین
        /// </summary>
        public string TargetAudienceDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO برای نمایش دفترچه در مرحله 2
    /// </summary>
    public class CashbackNotebookDto
    {
        /// <summary>
        /// شناسه دفترچه
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// نام دفترچه
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// تعداد اعضا
        /// </summary>
        public int MembersCount { get; set; }
    }

    /// <summary>
    /// DTO برای مرحله 3 ایجاد کش‌بک (اطلاعات تکمیلی)
    /// </summary>
    public class CashbackStep3Dto
    {
        /// <summary>
        /// وضعیت کش‌بک (فعال/غیرفعال)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// زمان واریز کش‌بک (DateTime - برای زمان‌بندی شده)
        /// </summary>
        public DateTime? ScheduledDepositDateTime { get; set; }

        /// <summary>
        /// ارسال برای تگ‌های خاص
        /// </summary>
        public bool SendToSpecificTags { get; set; } = false;

        /// <summary>
        /// لیست شناسه تگ‌ها (برای فیلتر بر اساس تگ)
        /// </summary>
        public List<int>? TargetTagIds { get; set; }
    }

    /// <summary>
    /// درخواست دریافت خلاصه کش‌بک
    /// </summary>
    public class GetCashbackSummaryRequestDto
    {
        /// <summary>
        /// شناسه Draft (پیشنهادی - در صورت وجود)
        /// </summary>
        public string? DraftId { get; set; }

        /// <summary>
        /// اطلاعات مرحله 1 (در صورت عدم استفاده از draftId)
        /// </summary>
        public CashbackStep1Dto? Step1 { get; set; }

        /// <summary>
        /// اطلاعات مرحله 2 (در صورت عدم استفاده از draftId)
        /// </summary>
        public CashbackStep2Dto? Step2 { get; set; }
    }

    /// <summary>
    /// DTO برای خلاصه کامل کش‌بک (مرحله 3)
    /// </summary>
    public class CashbackFinalSummaryDto
    {
        /// <summary>
        /// نوع کش‌بک
        /// </summary>
        public string CashbackType { get; set; } = string.Empty;

        /// <summary>
        /// توضیح نوع کش‌بک
        /// </summary>
        public string CashbackTypeDescription { get; set; } = string.Empty;

        /// <summary>
        /// زمان اجرا/واریز
        /// </summary>
        public string ExecutionTime { get; set; } = string.Empty;

        /// <summary>
        /// تعداد مخاطبین
        /// </summary>
        public int ContactsCount { get; set; }

        /// <summary>
        /// هزینه هر پارت (تومان) - هزینه ارسال پیامک برای هر مخاطب
        /// </summary>
        public decimal CostPerPart { get; set; }

        /// <summary>
        /// هزینه کل تخمینی (تومان)
        /// </summary>
        public decimal EstimatedTotalCost { get; set; }

        /// <summary>
        /// هزینه فرمت شده
        /// </summary>
        public string FormattedEstimatedCost { get; set; } = string.Empty;

        /// <summary>
        /// وضعیت کیف پول (کافی/ناکافی)
        /// </summary>
        public string WalletStatus { get; set; } = string.Empty;

        /// <summary>
        /// آیا موجودی کافی است؟
        /// </summary>
        public bool HasSufficientBalance { get; set; }

        /// <summary>
        /// موجودی فعلی کیف پول
        /// </summary>
        public decimal CurrentWalletBalance { get; set; }

        /// <summary>
        /// موجودی فرمت شده
        /// </summary>
        public string FormattedWalletBalance { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO برای خلاصه ساده کش‌بک (برای نمایش در پایین صفحه مرحله 3)
    /// </summary>
    public class CashbackSummaryDto
    {
        /// <summary>
        /// نوع کش‌بک (برای نمایش: "درصدی" یا "مبلغ ثابت")
        /// </summary>
        public string CashbackType { get; set; } = string.Empty;

        /// <summary>
        /// درصد کش‌بک (برای نمایش: "۵%")
        /// </summary>
        public string Percentage { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ ثابت کش‌بک (برای نمایش: "۵۰,۰۰۰ تومان")
        /// </summary>
        public string FixedAmount { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ کل خرید (برای نمایش: "۵۰۰,۰۰۰ تومان")
        /// </summary>
        public string TotalPurchaseAmount { get; set; } = string.Empty;

        /// <summary>
        /// اعتبار کش‌بک (برای نمایش: "۳۰ روز")
        /// </summary>
        public string CashbackValidity { get; set; } = string.Empty;

        /// <summary>
        /// مخاطبین (برای نمایش: "همه مخاطبین" یا "دفترچه خاص" و ...)
        /// </summary>
        public string Audience { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO برای ذخیره تنظیمات مرحله 3 (فیلتر تگ و زمان ارسال)
    /// </summary>
    public class SaveCashbackStep3SettingsDto
    {
        /// <summary>
        /// زمان واریز کش‌بک (DateTime - برای زمان‌بندی شده)
        /// </summary>
        public DateTime? ScheduledDepositDateTime { get; set; }

        /// <summary>
        /// ارسال برای تگ‌های خاص
        /// </summary>
        public bool SendToSpecificTags { get; set; } = false;

        /// <summary>
        /// لیست شناسه تگ‌ها (برای فیلتر بر اساس تگ)
        /// </summary>
        public List<int>? TargetTagIds { get; set; }

        /// <summary>
        /// وضعیت کش‌بک (فعال/غیرفعال)
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// درخواست ذخیره تنظیمات مرحله 3
    /// </summary>
    public class SaveCashbackStep3RequestDto
    {
        /// <summary>
        /// شناسه Draft (پیشنهادی)
        /// </summary>
        public string? DraftId { get; set; }

        /// <summary>
        /// اطلاعات مرحله 1 (در صورت عدم استفاده از draftId)
        /// </summary>
        public CashbackStep1Dto? Step1 { get; set; }

        /// <summary>
        /// اطلاعات مرحله 2 (در صورت عدم استفاده از draftId)
        /// </summary>
        public CashbackStep2Dto? Step2 { get; set; }

        /// <summary>
        /// تنظیمات مرحله 3
        /// </summary>
        [Required(ErrorMessage = "تنظیمات مرحله 3 الزامی است")]
        public SaveCashbackStep3SettingsDto Settings { get; set; } = null!;
    }

    /// <summary>
    /// درخواست ایجاد کش‌بک جدید
    /// </summary>
    public class CreateCashbackDto
    {
        /// <summary>
        /// شناسه Draft (پیشنهادی - در صورت وجود، داده‌های مرحله 1 و 2 از draft خوانده می‌شود)
        /// </summary>
        public string? DraftId { get; set; }

        /// <summary>
        /// عنوان کش‌بک
        /// </summary>
        [Required(ErrorMessage = "عنوان کش‌بک الزامی است")]
        [MaxLength(200, ErrorMessage = "عنوان کش‌بک نباید بیشتر از 200 کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات کش‌بک
        /// </summary>
        [MaxLength(500, ErrorMessage = "توضیحات نباید بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>
        /// نوع کش‌بک (Percentage: درصدی، FixedAmount: مبلغ ثابت)
        /// </summary>
        [Required(ErrorMessage = "نوع کش‌بک الزامی است")]
        public string CashbackType { get; set; } = "Percentage";

        /// <summary>
        /// درصد کش‌بک (برای نوع درصدی) - بین 1 تا 50
        /// </summary>
        [Range(1, 50, ErrorMessage = "درصد کش‌بک باید بین 1 تا 50 باشد")]
        public decimal? Percentage { get; set; }

        /// <summary>
        /// مبلغ ثابت کش‌بک (برای نوع مبلغ ثابت) - تومان
        /// </summary>
        [Range(1000, 10000000, ErrorMessage = "مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد")]
        public decimal? FixedAmount { get; set; }

        /// <summary>
        /// حداکثر مبلغ کش‌بک برای هر خرید (تومان)
        /// </summary>
        [Range(0, 10000000, ErrorMessage = "حداکثر مبلغ کش‌بک نامعتبر است")]
        public decimal? MaxCashbackAmount { get; set; }

        /// <summary>
        /// حداقل مبلغ خرید برای دریافت کش‌بک (تومان)
        /// </summary>
        [Range(0, 100000000, ErrorMessage = "حداقل مبلغ خرید نامعتبر است")]
        public decimal? MinPurchaseAmount { get; set; }

        /// <summary>
        /// مدت اعتبار به روز (1 تا 365 روز)
        /// </summary>
        [Range(1, 365, ErrorMessage = "مدت اعتبار باید بین 1 تا 365 روز باشد")]
        public int ValidityDays { get; set; } = 30;

        /// <summary>
        /// زمان واریز کش‌بک (Immediate: فوری، Scheduled: زمان‌بندی شده)
        /// </summary>
        public string DepositTiming { get; set; } = "Immediate";

        /// <summary>
        /// زمان مشخص واریز (ساعت:دقیقه) - برای زمان‌بندی روزانه
        /// </summary>
        public string? ScheduledDepositTime { get; set; }

        /// <summary>
        /// تاریخ و زمان دقیق واریز زمان‌بندی شده (UTC) - برای زمان‌بندی یکباره
        /// </summary>
        public DateTime? ScheduledDepositDateTime { get; set; }

        /// <summary>
        /// نوع مخاطبین (All: همه، NewContacts: مخاطبین جدید، SpecificNotebooks: دفترچه خاص)
        /// </summary>
        [Required(ErrorMessage = "نوع مخاطبین الزامی است")]
        public string TargetAudience { get; set; } = "All";

        /// <summary>
        /// لیست شناسه دفترچه‌ها (برای نوع دفترچه خاص)
        /// </summary>
        public List<int>? TargetNotebookIds { get; set; }

        /// <summary>
        /// ارسال برای تگ‌های خاص
        /// </summary>
        public bool SendToSpecificTags { get; set; } = false;

        /// <summary>
        /// لیست شناسه تگ‌ها (برای فیلتر بر اساس تگ)
        /// </summary>
        public List<int>? TargetTagIds { get; set; }

        /// <summary>
        /// فعال بودن کش‌بک از ابتدا
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// درخواست اعمال کش‌بک به یک مخاطب خاص (حالت خرید در مغازه)
    /// </summary>
    public class ApplyCashbackToContactDto
    {
        /// <summary>
        /// شماره موبایل مخاطب
        /// </summary>
        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        public string MobileNumber { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ خرید (برای کش‌بک درصدی الزامی است)
        /// </summary>
        [Range(0, 100000000, ErrorMessage = "مبلغ خرید نامعتبر است")]
        public decimal PurchaseAmount { get; set; }

        /// <summary>
        /// شناسه کش‌بک (اختیاری - اگر ارسال نشود، از کش‌بک‌های فعال استفاده می‌شود)
        /// </summary>
        public int? CashbackId { get; set; }
    }

    /// <summary>
    /// نتیجه اعمال کش‌بک به یک مخاطب
    /// </summary>
    public class ApplyCashbackToContactResultDto
    {
        /// <summary>
        /// آیا اعمال موفق بود؟
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// مبلغ کش‌بک
        /// </summary>
        public decimal CashbackAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedCashbackAmount { get; set; } = string.Empty;

        /// <summary>
        /// شناسه تراکنش کش‌بک
        /// </summary>
        public int? CashbackTransactionId { get; set; }

        /// <summary>
        /// شناسه کش‌بک استفاده شده
        /// </summary>
        public int? CashbackId { get; set; }

        /// <summary>
        /// عنوان کش‌بک
        /// </summary>
        public string? CashbackTitle { get; set; }

        /// <summary>
        /// موجودی کش‌بک قبل از افزودن (تومان)
        /// </summary>
        public decimal PreviousBalance { get; set; }

        /// <summary>
        /// موجودی کش‌بک بعد از افزودن (تومان)
        /// </summary>
        public decimal NewBalance { get; set; }

        /// <summary>
        /// موجودی قبلی فرمت شده
        /// </summary>
        public string FormattedPreviousBalance { get; set; } = "0 تومان";

        /// <summary>
        /// موجودی جدید فرمت شده
        /// </summary>
        public string FormattedNewBalance { get; set; } = string.Empty;

        /// <summary>
        /// آیا مخاطب قبلاً موجودی کش‌بک داشت؟
        /// </summary>
        public bool HadPreviousBalance { get; set; }
    }

    /// <summary>
    /// نتیجه اعمال کش‌بک
    /// </summary>
    public class ApplyCashbackResultDto
    {
        /// <summary>
        /// تعداد کل مخاطبین
        /// </summary>
        public int TotalContacts { get; set; }

        /// <summary>
        /// تعداد موفق
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// تعداد ناموفق
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// مجموع مبلغ کش‌بک واریز شده
        /// </summary>
        public decimal TotalCashbackAmount { get; set; }

        /// <summary>
        /// مبلغ فرمت شده
        /// </summary>
        public string FormattedTotalCashbackAmount { get; set; } = string.Empty;

        /// <summary>
        /// هزینه ارسال پیامک
        /// </summary>
        public decimal SmsCost { get; set; }

        /// <summary>
        /// هزینه فرمت شده
        /// </summary>
        public string FormattedSmsCost { get; set; } = string.Empty;
    }

    /// <summary>
    /// درخواست به‌روزرسانی کش‌بک
    /// </summary>
    public class UpdateCashbackDto
    {
        /// <summary>
        /// عنوان کش‌بک
        /// </summary>
        [MaxLength(200, ErrorMessage = "عنوان کش‌بک نباید بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        /// <summary>
        /// توضیحات کش‌بک
        /// </summary>
        [MaxLength(500, ErrorMessage = "توضیحات نباید بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>
        /// درصد کش‌بک (برای نوع درصدی)
        /// </summary>
        [Range(1, 50, ErrorMessage = "درصد کش‌بک باید بین 1 تا 50 باشد")]
        public decimal? Percentage { get; set; }

        /// <summary>
        /// مبلغ ثابت کش‌بک (برای نوع مبلغ ثابت)
        /// </summary>
        [Range(1000, 10000000, ErrorMessage = "مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد")]
        public decimal? FixedAmount { get; set; }

        /// <summary>
        /// حداکثر مبلغ کش‌بک برای هر خرید
        /// </summary>
        [Range(0, 10000000, ErrorMessage = "حداکثر مبلغ کش‌بک نامعتبر است")]
        public decimal? MaxCashbackAmount { get; set; }

        /// <summary>
        /// حداقل مبلغ خرید برای دریافت کش‌بک
        /// </summary>
        [Range(0, 100000000, ErrorMessage = "حداقل مبلغ خرید نامعتبر است")]
        public decimal? MinPurchaseAmount { get; set; }

        /// <summary>
        /// مدت اعتبار به روز
        /// </summary>
        [Range(1, 365, ErrorMessage = "مدت اعتبار باید بین 1 تا 365 روز باشد")]
        public int? ValidityDays { get; set; }

        /// <summary>
        /// ارسال برای تگ‌های خاص
        /// </summary>
        public bool? SendToSpecificTags { get; set; }

        /// <summary>
        /// لیست شناسه تگ‌ها
        /// </summary>
        public List<int>? TargetTagIds { get; set; }
    }

    /// <summary>
    /// درخواست تغییر وضعیت کش‌بک
    /// </summary>
    public class ToggleCashbackStatusDto
    {
        /// <summary>
        /// فعال یا غیرفعال
        /// </summary>
        [Required(ErrorMessage = "وضعیت الزامی است")]
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO برای ذخیره draft کش‌بک (مرحله 1 و 2)
    /// </summary>
    public class SaveCashbackDraftDto
    {
        /// <summary>
        /// اطلاعات مرحله 1
        /// </summary>
        [Required(ErrorMessage = "اطلاعات مرحله 1 الزامی است")]
        public CashbackStep1Dto Step1 { get; set; } = null!;

        /// <summary>
        /// اطلاعات مرحله 2
        /// </summary>
        [Required(ErrorMessage = "اطلاعات مرحله 2 الزامی است")]
        public CashbackStep2Dto Step2 { get; set; } = null!;
    }

    /// <summary>
    /// پاسخ ذخیره draft
    /// </summary>
    public class CashbackDraftResponseDto
    {
        /// <summary>
        /// شناسه draft
        /// </summary>
        public string DraftId { get; set; } = string.Empty;

        /// <summary>
        /// زمان انقضا draft (UTC)
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// DTO برای دریافت draft
    /// </summary>
    public class CashbackDraftDto
    {
        /// <summary>
        /// اطلاعات مرحله 1
        /// </summary>
        public CashbackStep1Dto Step1 { get; set; } = null!;

        /// <summary>
        /// اطلاعات مرحله 2
        /// </summary>
        public CashbackStep2Dto Step2 { get; set; } = null!;
    }

    #endregion
}




