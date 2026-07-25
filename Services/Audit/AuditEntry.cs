using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api_Vapp.Services.Audit
{
    /// <summary>ورودی استاندارد ثبت audit.</summary>
    public sealed class AuditEntry
    {
        public required string Category { get; init; }
        public required string Action { get; init; }
        public required string EntityType { get; init; }
        public string? EntityId { get; init; }
        public int? ActorUserId { get; init; }
        public int? TargetUserId { get; init; }

        /// <summary>object یا string از قبل serialize‌شده</summary>
        public object? Before { get; init; }

        public object? After { get; init; }
        public object? Metadata { get; init; }

        public string? CorrelationId { get; init; }
        public string? IpAddress { get; init; }
        public string? UserAgent { get; init; }
        public string? RequestPath { get; init; }
        public string? HttpMethod { get; init; }
        public string? Source { get; init; }
        public bool Succeeded { get; init; } = true;
        public string? ErrorMessage { get; init; }
    }

    public static class AuditJson
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public static string? Serialize(object? value)
        {
            if (value == null)
                return null;

            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? null : s;

            try
            {
                return JsonSerializer.Serialize(value, Options);
            }
            catch
            {
                return value.ToString();
            }
        }
    }
}
