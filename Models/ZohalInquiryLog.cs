namespace Api_Vapp.Models
{
    /// <summary>
    /// لاگ immutable استعلام‌های زحل (شاهکار و ...) — فقط append.
    /// </summary>
    public class ZohalInquiryLog
    {
        public long Id { get; set; }

        /// <summary>نوع استعلام — shahkar</summary>
        public string InquiryType { get; set; } = "shahkar";

        /// <summary>منبع فراخوانی — register</summary>
        public string Source { get; set; } = string.Empty;

        public string MobileMasked { get; set; } = string.Empty;

        public string NationalCodeMasked { get; set; } = string.Empty;

        /// <summary>null = نامشخص / خطا قبل از پاسخ</summary>
        public bool? Matched { get; set; }

        public int? HttpStatusCode { get; set; }

        /// <summary>فیلد result در پاسخ زحل</summary>
        public int? ZohalResultCode { get; set; }

        public string? ProviderErrorCode { get; set; }

        /// <summary>پیام خام زحل — فقط برای پشتیبانی</summary>
        public string? ProviderMessage { get; set; }

        /// <summary>Matched | NotMatched | InvalidInput | ServiceUnavailable | ...</summary>
        public string OutcomeStatus { get; set; } = string.Empty;

        public string? UserFacingErrorCode { get; set; }

        /// <summary>JSON درخواست (ماسک‌شده)</summary>
        public string? RequestJson { get; set; }

        /// <summary>JSON پاسخ کامل زحل</summary>
        public string? ResponseJson { get; set; }

        public int DurationMs { get; set; }

        public bool Succeeded { get; set; }

        public string? TraceId { get; set; }

        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
