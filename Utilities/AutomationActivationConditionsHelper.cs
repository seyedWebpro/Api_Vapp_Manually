using System.Text.Json;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// پارس JSON تنظیمات ActivationConditions برای پیام‌های خودکار
    /// </summary>
    public static class AutomationActivationConditionsHelper
    {
        public const string ExecutionModeOnce = "Once";
        public const string ExecutionModeMultiple = "Multiple";

        private static readonly HashSet<string> RecognizedCustomConditionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "executionMode",
            "daysWithoutPurchase",
            "daysSinceContactCreated",
            "minCashbackBalance",
            "maxCashbackBalance",
            "hasCashback",
            "hasDateOfBirth",
            "minDaysUntilCashbackExpiry",
            "maxDaysUntilCashbackExpiry"
        };

        public static string ReadExecutionMode(string? activationConditionsJson, string defaultValue = ExecutionModeOnce)
        {
            if (string.IsNullOrWhiteSpace(activationConditionsJson))
                return defaultValue;

            try
            {
                using var doc = JsonDocument.Parse(activationConditionsJson);
                if (TryReadStringProperty(doc.RootElement, "executionMode", out var mode)
                    && IsValidExecutionMode(mode))
                {
                    return mode!;
                }
            }
            catch
            {
                // ignore malformed JSON — use default
            }

            return defaultValue;
        }

        public static bool TryParseCustomConditions(string? activationConditionsJson, out CustomAutomationConditions conditions)
        {
            conditions = new CustomAutomationConditions();

            if (string.IsNullOrWhiteSpace(activationConditionsJson))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(activationConditionsJson);
                var root = doc.RootElement;

                conditions.ExecutionMode = ReadExecutionMode(activationConditionsJson, ExecutionModeOnce);

                if (TryReadIntProperty(root, "daysWithoutPurchase", out var daysWithoutPurchase))
                    conditions.DaysWithoutPurchase = daysWithoutPurchase;

                if (TryReadIntProperty(root, "daysSinceContactCreated", out var daysSinceCreated))
                    conditions.DaysSinceContactCreated = daysSinceCreated;

                if (TryReadDecimalProperty(root, "minCashbackBalance", out var minCashback))
                    conditions.MinCashbackBalance = minCashback;

                if (TryReadDecimalProperty(root, "maxCashbackBalance", out var maxCashback))
                    conditions.MaxCashbackBalance = maxCashback;

                if (TryReadBoolProperty(root, "hasCashback", out var hasCashback))
                    conditions.HasCashback = hasCashback;

                if (TryReadBoolProperty(root, "hasDateOfBirth", out var hasDateOfBirth))
                    conditions.HasDateOfBirth = hasDateOfBirth;

                if (TryReadIntProperty(root, "minDaysUntilCashbackExpiry", out var minDaysUntilExpiry))
                    conditions.MinDaysUntilCashbackExpiry = minDaysUntilExpiry;

                if (TryReadIntProperty(root, "maxDaysUntilCashbackExpiry", out var maxDaysUntilExpiry))
                    conditions.MaxDaysUntilCashbackExpiry = maxDaysUntilExpiry;

                return conditions.HasAnyRule;
            }
            catch
            {
                conditions = new CustomAutomationConditions();
                return false;
            }
        }

        public static bool ContainsOnlyUnrecognizedCustomKeys(string? activationConditionsJson)
        {
            if (string.IsNullOrWhiteSpace(activationConditionsJson))
                return true;

            try
            {
                using var doc = JsonDocument.Parse(activationConditionsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return true;

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (RecognizedCustomConditionKeys.Contains(property.Name))
                        return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        public static bool IsValidExecutionMode(string? mode) =>
            string.Equals(mode, ExecutionModeOnce, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, ExecutionModeMultiple, StringComparison.OrdinalIgnoreCase);

        private static bool TryReadStringProperty(JsonElement root, string propertyName, out string? value)
        {
            value = null;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static bool TryReadIntProperty(JsonElement root, string propertyName, out int value)
        {
            value = 0;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
                return true;

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
                return true;

            return false;
        }

        private static bool TryReadDecimalProperty(JsonElement root, string propertyName, out decimal value)
        {
            value = 0;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out value))
                return true;

            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out value))
                return true;

            return false;
        }

        private static bool TryReadBoolProperty(JsonElement root, string propertyName, out bool value)
        {
            value = false;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (prop.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out value))
                return true;

            return false;
        }
    }

    /// <summary>
    /// شرایط قابل اجرا برای اتوماسیون سفارشی — همه شرط‌های تعریف‌شده با AND ارزیابی می‌شوند.
    /// </summary>
    public sealed class CustomAutomationConditions
    {
        public string ExecutionMode { get; set; } = AutomationActivationConditionsHelper.ExecutionModeOnce;

        public int? DaysWithoutPurchase { get; set; }

        public int? DaysSinceContactCreated { get; set; }

        public decimal? MinCashbackBalance { get; set; }

        public decimal? MaxCashbackBalance { get; set; }

        public bool? HasCashback { get; set; }

        public bool? HasDateOfBirth { get; set; }

        public int? MinDaysUntilCashbackExpiry { get; set; }

        public int? MaxDaysUntilCashbackExpiry { get; set; }

        public bool HasAnyRule =>
            DaysWithoutPurchase.HasValue
            || DaysSinceContactCreated.HasValue
            || MinCashbackBalance.HasValue
            || MaxCashbackBalance.HasValue
            || HasCashback.HasValue
            || HasDateOfBirth.HasValue
            || MinDaysUntilCashbackExpiry.HasValue
            || MaxDaysUntilCashbackExpiry.HasValue;
    }
}
