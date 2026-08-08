using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// همیشه عدد اعشاری در JSON می‌نویسد (مثلاً 76.0 نه 76) تا کلاینت موبایل double ببیند.
    /// </summary>
    public sealed class JsonAlwaysDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var d))
                return d;

            if (reader.TokenType == JsonTokenType.String
                && double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;

            throw new JsonException("Expected a number for double.");
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            writer.WriteRawValue(rounded.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }
}
