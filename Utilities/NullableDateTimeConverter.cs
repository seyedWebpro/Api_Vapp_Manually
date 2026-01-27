using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// JsonConverter برای تبدیل رشته خالی به null در DateTime?
    /// </summary>
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                
                // اگر رشته خالی یا null باشد، null برگردان
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }

                // تلاش برای تبدیل به DateTime با حفظ timezone
                // ابتدا سعی می‌کنیم DateTimeOffset را parse کنیم (که timezone را حفظ می‌کند)
                if (DateTimeOffset.TryParse(stringValue, out var dateTimeOffset))
                {
                    // تبدیل به UTC و سپس به DateTime
                    return dateTimeOffset.UtcDateTime;
                }
                
                // اگر DateTimeOffset parse نشد، از DateTime استفاده می‌کنیم
                if (DateTime.TryParse(stringValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dateTime))
                {
                    // اگر Kind مشخص نیست، فرض می‌کنیم UTC است
                    if (dateTime.Kind == DateTimeKind.Unspecified)
                    {
                        // اگر timezone در رشته وجود دارد، از DateTimeOffset استفاده می‌کنیم
                        if (stringValue.Contains('+') || stringValue.Contains('-') && stringValue.Length > 10)
                        {
                            if (DateTimeOffset.TryParse(stringValue, out var dto))
                            {
                                return dto.UtcDateTime;
                            }
                        }
                        // در غیر این صورت، فرض می‌کنیم UTC است
                        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                    }
                    // اگر Local است، به UTC تبدیل می‌کنیم
                    if (dateTime.Kind == DateTimeKind.Local)
                    {
                        return dateTime.ToUniversalTime();
                    }
                    // اگر UTC است، همان را برمی‌گردانیم
                    return dateTime;
                }

                // اگر تبدیل نشد، null برگردان
                return null;
            }

            // برای سایر انواع (مثلاً number)، از converter پیش‌فرض استفاده کن
            if (reader.TokenType == JsonTokenType.Number)
            {
                // اگر عدد است، احتمالاً timestamp است
                var timestamp = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}

