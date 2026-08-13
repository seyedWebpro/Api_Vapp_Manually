using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// JsonConverter برای پذیرش تاریخ شمسی (مثل 1405/05/19) در کنار تاریخ میلادی/ISO.
    /// رشته‌های فقط-تاریخ (بدون زمان) به‌صورت نیمه‌شب UTC همان روز تقویمی تفسیر می‌شوند
    /// تا شیفت timezone محلی (مثلاً ایران) یک روز عقب نیندازد.
    /// </summary>
    public class FlexibleDateTimeConverter : JsonConverter<DateTime?>
    {
        private static readonly PersianCalendar PersianCalendar = new();
        private static readonly Regex DateOnlyRegex = new(
            @"^(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime.EnsureDateOnlyUtc();

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
        /// تبدیل رشته تاریخ (شمسی یا میلادی) به DateTime با Kind=Utc.
        /// برای فیلدهای تقویمی، خروجی تاریخ‌محور (نیمه‌شب UTC) است.
        /// </summary>
        public static DateTime? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = NormalizeDigits(value.Trim());

            if (TryParsePersian(normalized, out var persianDate))
                return persianDate.EnsureDateOnlyUtc();

            // YYYY-MM-DD بدون زمان → نیمه‌شب UTC همان روز (جلوگیری از شیفت timezone محلی)
            if (TryParseGregorianDateOnly(normalized, out var dateOnly))
                return dateOnly;

            if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dto))
                return dto.UtcDateTime.EnsureDateOnlyUtc();

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt.EnsureDateOnlyUtc();

            return null;
        }

        /// <summary>
        /// فقط وقتی رشته دقیقاً تاریخ است (بدون جزء زمان) — مثل 2026-08-13
        /// </summary>
        private static bool TryParseGregorianDateOnly(string value, out DateTime result)
        {
            result = default;
            var match = DateOnlyRegex.Match(value);
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups["y"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                || !int.TryParse(match.Groups["m"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var month)
                || !int.TryParse(match.Groups["d"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var day))
                return false;

            if (year < 1800 || year > 2200 || month < 1 || month > 12)
                return false;

            var daysInMonth = DateTime.DaysInMonth(year, month);
            if (day < 1 || day > daysInMonth)
                return false;

            result = DateTime.SpecifyKind(new DateTime(year, month, day), DateTimeKind.Utc);
            return true;
        }

        private static bool TryParsePersian(string value, out DateTime result)
        {
            result = default;

            var datePart = value.Split(' ', 'T')[0];
            var parts = datePart.Split('/', '-');
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
                return false;

            if (year < 1200 || year > 1500)
                return false;

            if (month < 1 || month > 12 || day < 1 || day > PersianCalendar.GetDaysInMonth(year, month))
                return false;

            result = DateTime.SpecifyKind(
                PersianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0),
                DateTimeKind.Utc);
            return true;
        }

        private static string NormalizeDigits(string value)
        {
            Span<char> buffer = stackalloc char[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                buffer[i] = c switch
                {
                    >= '۰' and <= '۹' => (char)(c - '۰' + '0'),
                    >= '٠' and <= '٩' => (char)(c - '٠' + '0'),
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
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime.EnsureDateOnlyUtc();
            }

            throw new JsonException(
                "فرمت تاریخ نامعتبر است. تاریخ شمسی (1405/05/19) یا میلادی (2026-08-10) ارسال کنید");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
