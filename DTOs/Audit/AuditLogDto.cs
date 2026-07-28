namespace Api_Vapp.DTOs.Audit
{
    public class AuditLogDto
    {
        public long Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public int? ActorUserId { get; set; }
        public int? TargetUserId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Metadata { get; set; }
        public string? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? RequestPath { get; set; }
        public string? HttpMethod { get; set; }
        public string Source { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>زمان تهران برای خوانایی در دیباگ</summary>
        public DateTime CreatedAtTehran { get; set; }
    }

    public class AuditSearchRequestDto
    {
        public string? Category { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public int? ActorUserId { get; set; }
        public int? TargetUserId { get; set; }
        public string? CorrelationId { get; set; }
        public string? Source { get; set; }
        public bool? Succeeded { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }

        /// <summary>جستجوی متنی روی Action / EntityId / CorrelationId / ErrorMessage</summary>
        public string? Q { get; set; }

        /// <summary>
        /// جستجو داخل OldValue/NewValue/Metadata (JSON).
        /// کندتر است؛ برای کیس‌هایی مثل «قیمت ۱۹ میلیون» استفاده شود.
        /// اگر Full-Text فعال باشد از CONTAINS استفاده می‌شود.
        /// </summary>
        public bool SearchInJson { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
