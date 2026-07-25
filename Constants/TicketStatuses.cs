namespace Api_Vapp.Constants
{
    public static class TicketStatuses
    {
        public const string Open = "Open";
        public const string InProgress = "InProgress";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Open, InProgress, Resolved, Closed
        };

        public static readonly IReadOnlySet<string> ClosedLike = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Resolved, Closed
        };

        public static bool IsKnown(string? status) =>
            !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());

        public static bool IsClosedLike(string? status) =>
            !string.IsNullOrWhiteSpace(status) && ClosedLike.Contains(status.Trim());

        public static string GetPersianLabel(string? status) => status?.Trim() switch
        {
            Open => "در انتظار پاسخ",
            InProgress => "پاسخ داده شده",
            Resolved => "حل شده",
            Closed => "بسته شده",
            _ => status ?? string.Empty
        };
    }

    public static class TicketPriorities
    {
        public const string Low = "Low";
        public const string Normal = "Normal";
        public const string High = "High";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Low, Normal, High
        };

        public static bool IsKnown(string? priority) =>
            !string.IsNullOrWhiteSpace(priority) && All.Contains(priority.Trim());

        public static string Normalize(string? priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
                return Normal;

            var trimmed = priority.Trim();
            return All.FirstOrDefault(p => p.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ?? Normal;
        }

        public static string GetPersianLabel(string? priority) => priority?.Trim() switch
        {
            Low => "کم",
            Normal => "متوسط",
            High => "بالا",
            _ => priority ?? string.Empty
        };
    }
}

