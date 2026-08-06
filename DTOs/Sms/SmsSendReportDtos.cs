using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// فیلتر لیست ارسال‌ها (صفحه اول گزارش)
    /// </summary>
    public class SmsSendListFilterDto
    {
        /// <summary>جستجو بر اساس عنوان یا کد ارسال (Sid)</summary>
        [MaxLength(200, ErrorMessage = "عبارت جستجو نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string? Search { get; set; }

        /// <summary>All | Campaign | Cashback | Reward</summary>
        [MaxLength(50, ErrorMessage = "نوع ارسال نامعتبر است")]
        public string? SendType { get; set; }

        /// <summary>Last7Days | Last30Days | Last90Days | Custom</summary>
        [MaxLength(50, ErrorMessage = "بازه زمانی نامعتبر است")]
        public string? DateRangePreset { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "شماره صفحه باید حداقل ۱ باشد")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "اندازه صفحه باید بین ۱ تا ۱۰۰ باشد")]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// فیلتر لیست مخاطبین یک ارسال
    /// </summary>
    public class SmsSendRecipientFilterDto
    {
        [MaxLength(50, ErrorMessage = "عبارت جستجو نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string? Search { get; set; }

        [MaxLength(50, ErrorMessage = "وضعیت دلیوری نامعتبر است")]
        public string? DeliveryCategory { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "شماره صفحه باید حداقل ۱ باشد")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "اندازه صفحه باید بین ۱ تا ۱۰۰ باشد")]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// یک ردیف لیست ارسال‌ها
    /// </summary>
    public class SmsSendBatchListItemDto
    {
        /// <summary>
        /// کد ارسال نماینده (برای کمپین: Min(Sid) گیرندگان — برای مسیر /sends/{sid} کافی است)
        /// </summary>
        public long Sid { get; set; }

        /// <summary>
        /// شناسه پایدار دسته ارسال — برای کمپین برابر شناسه کمپین، برای بقیه برابر Sid
        /// </summary>
        public long SendId { get; set; }

        /// <summary>
        /// true وقتی ردیف یک کمپین چندگیرنده است (جزئیات/اکسل باید کل کمپین را نشان دهد)
        /// </summary>
        public bool IsCampaignBatch { get; set; }

        public string Title { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string SourceModuleLabel { get; set; } = string.Empty;
        public string SendType { get; set; } = string.Empty;
        public string SendTypeLabel { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public int SendCount { get; set; }
        public int PartsCount { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class SmsSendBatchListDto
    {
        public List<SmsSendBatchListItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// هدر جزئیات یک ارسال (بالای صفحه جزئیات)
    /// </summary>
    public class SmsSendBatchDetailDto
    {
        public long Sid { get; set; }
        public long SendId { get; set; }
        public bool IsCampaignBatch { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string SourceModuleLabel { get; set; } = string.Empty;
        public string SendType { get; set; } = string.Empty;
        public string SendTypeLabel { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public string SenderNumber { get; set; } = string.Empty;
        public int SendCount { get; set; }
        public int PartsCount { get; set; }
        public DateTime SentAt { get; set; }
        public string? MessageText { get; set; }
        public SmsDeliverySummaryDto Summary { get; set; } = new();
    }

    /// <summary>
    /// یک ردیف مخاطب در جزئیات ارسال
    /// </summary>
    public class SmsSendRecipientDto
    {
        public int Id { get; set; }
        public int RowNumber { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string SenderNumber { get; set; } = string.Empty;
        public string DeliveryCategory { get; set; } = string.Empty;
        public string DeliveryCategoryLabel { get; set; } = string.Empty;
        public int? ProviderStatusCode { get; set; }
        public string? ProviderStatusMessage { get; set; }
        public bool IsDeliveryFinal { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? LastCheckedAt { get; set; }
    }

    public class SmsSendRecipientListDto
    {
        public long Sid { get; set; }
        public long SendId { get; set; }
        public bool IsCampaignBatch { get; set; }
        public List<SmsSendRecipientDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// جزئیات یک پیامک (مودال کلیک روی مخاطب)
    /// </summary>
    public class SmsMessageDetailDto
    {
        public int Id { get; set; }
        public long Sid { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string SenderNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string SourceModuleLabel { get; set; } = string.Empty;
        public string DeliveryCategory { get; set; } = string.Empty;
        public string DeliveryCategoryLabel { get; set; } = string.Empty;
        public string? StatusHint { get; set; }
        public DateTime SentAt { get; set; }
        public string? MessageText { get; set; }
    }

    /// <summary>
    /// گزینه‌های فیلتر برای UI
    /// </summary>
    public class SmsReportFilterOptionsDto
    {
        public List<SmsReportFilterOptionDto> SendTypes { get; set; } = new();
        public List<SmsReportFilterOptionDto> DateRangePresets { get; set; } = new();
        public List<SmsReportFilterOptionDto> DeliveryCategories { get; set; } = new();
    }

    public class SmsReportFilterOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Projection داخلی برای GroupBy در Repository
    /// </summary>
    public class SmsSendBatchProjection
    {
        public long Sid { get; set; }
        public long SendId { get; set; }
        public bool IsCampaignBatch { get; set; }
        public string? Title { get; set; }
        public string SourceModule { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public int SendCount { get; set; }
        public DateTime SentAt { get; set; }
    }
}
