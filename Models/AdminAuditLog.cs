namespace Api_Vapp.Models
{
    /// <summary>
    /// ردپای غیرقابل‌تغییر اکشن‌های حساس (ادمین / سیستم / بک‌گراند).
    /// استثنا از قانون SoftDelete/UpdatedAt مدل‌های معمولی — فقط append (immutable).
    /// </summary>
    public class AdminAuditLog
    {
        /// <summary>bigint برای حجم بالای لاگ</summary>
        public long Id { get; set; }

        /// <summary>گروه منطقی — مثلا Admin, Payment, Wallet</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>اکشن دقیق — مثلا SubscriptionPlan.PriceUpdated</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>نوع موجودیت — مثلا SubscriptionPlan</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>شناسه موجودیت به‌صورت string (int/guid/…)</summary>
        public string? EntityId { get; set; }

        /// <summary>کاربری که اکشن را انجام داده (ادمین یا سیستم null)</summary>
        public int? ActorUserId { get; set; }

        /// <summary>کاربر هدف اکشن (مثلاً بن شدن کاربر X)</summary>
        public int? TargetUserId { get; set; }

        /// <summary>اسنپ‌شات قبل — JSON</summary>
        public string? OldValue { get; set; }

        /// <summary>اسنپ‌شات بعد — JSON</summary>
        public string? NewValue { get; set; }

        /// <summary>دادهٔ تکمیلی — JSON (مبالغ، کدها، … بدون secret)</summary>
        public string? Metadata { get; set; }

        /// <summary>شناسه همبستگی درخواست (TraceId)</summary>
        public string? CorrelationId { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? RequestPath { get; set; }

        public string? HttpMethod { get; set; }

        /// <summary>منبع ثبت — Http | Background | System</summary>
        public string Source { get; set; } = AuditSources.Http;

        public bool Succeeded { get; set; } = true;

        public string? ErrorMessage { get; set; }

        /// <summary>UTC</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class AuditSources
    {
        public const string Http = "Http";
        public const string Background = "Background";
        public const string System = "System";
    }
}
