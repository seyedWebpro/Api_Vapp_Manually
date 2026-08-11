using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// JsonConverter برای پذیرش تاریخ شمسی (مثل 1405/05/19) در کنار تاریخ میلادی/ISO.
    /// تاریخ شمسی به میلادی UTC تبدیل می‌شود و خروجی همیشه میلادی است.
    /// </summary>
    public class FlexibleDateTimeConverter : JsonConverter<DateTime?>
    {
        private static readonly PersianCalendar PersianCalendar = new();

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                return Parse(value)
                    ?? throw new JsonException(
                        "فرمت تاریخ نامعتبر است. تاریخ شمسی (1405/05/19) یا میلادی (2026-08-10) ارسال کنید");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;

            throw new JsonException("فرمت تاریخ نامعتبر است");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value);
            else
                writer.WriteNullValue();
        }

        /// <summary>
        /// تبدیل رشته تاریخ (شمسی یا میلادی) به DateTime با Kind=Utc. در صورت ناموفق بودن null برمی‌گرداند.
        /// </summary>
        public static DateTime? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = NormalizeDigits(value.Trim());

            if (TryParsePersian(normalized, out var persianDate))
                return persianDate;

            if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                return dto.UtcDateTime;

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt.EnsureUtc();

            return null;
        }

        private static bool TryParsePersian(string value, out DateTime result)
        {
            result = default;

            // فقط بخش تاریخ را در نظر می‌گیریم (اگر زمان هم آمده باشد)
            var datePart = value.Split(' ', 'T')[0];
            var parts = datePart.Split('/', '-');
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
                return false;

            // بازه معتبر سال شمسی — سال میلادی (>= 1800) وارد این مسیر نمی‌شود
            if (year < 1200 || year > 1500)
                return false;

            if (month < 1 || month > 12 || day < 1 || day > PersianCalendar.GetDaysInMonth(year, month))
                return false;

            result = DateTime.SpecifyKind(
                PersianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0),
                DateTimeKind.Utc);
            return true;
        }

        /// <summary>
        /// تبدیل ارقام فارسی/عربی به ارقام لاتین
        /// </summary>
        private static string NormalizeDigits(string value)
        {
            Span<char> buffer = stackalloc char[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                buffer[i] = c switch
                {
                    >= '۰' and <= '۹' => (char)(c - '۰' + '0'), // ارقام فارسی
                    >= '٠' and <= '٩' => (char)(c - '٠' + '0'), // ارقام عربی
                    _ => c
                };
            }

            return new string(buffer);
        }
    }

    /// <summary>
    /// نسخه non-nullable از <see cref="FlexibleDateTimeConverter"/> برای فیلدهای تاریخ الزامی.
    /// </summary>
    public class FlexibleDateTimeRequiredConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var parsed = FlexibleDateTimeConverter.Parse(reader.GetString());
                if (parsed.HasValue)
                    return parsed.Value;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;
            }

            throw new JsonException(
                "فرمت تاریخ نامعتبر است. تاریخ شمسی (1405/05/19) یا میلادی (2026-08-10) ارسال کنید");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
