using System.Text.Json;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// سریالایز/دی‌سریالایز لیست شماره‌های NumberSeeker برای ذخیره پایدار در DB.
    /// </summary>
    public static class NumberSeekerPhoneStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string? Serialize(IReadOnlyList<string>? phones)
        {
            if (phones == null || phones.Count == 0)
                return null;

            var cleaned = phones
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned, JsonOptions);
        }

        public static List<string> Deserialize(string? phonesJson)
        {
            if (string.IsNullOrWhiteSpace(phonesJson))
                return new List<string>();

            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(phonesJson, JsonOptions);
                if (list == null || list.Count == 0)
                    return new List<string>();

                return list
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }
    }
}
