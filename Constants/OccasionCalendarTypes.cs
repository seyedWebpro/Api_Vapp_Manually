namespace Api_Vapp.Constants
{
    /// <summary>
    /// نوع تقویم مناسبت سالانه (تطبیق ماه/روز با «امروز تهران»)
    /// </summary>
    public static class OccasionCalendarTypes
    {
        public const string Jalali = "Jalali";
        public const string Gregorian = "Gregorian";
        public const string Hijri = "Hijri";

        public static readonly string[] All = [Jalali, Gregorian, Hijri];

        public static bool IsKnown(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && All.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        public static string Normalize(string? value) =>
            value?.Trim() switch
            {
                var v when string.Equals(v, Gregorian, StringComparison.OrdinalIgnoreCase) => Gregorian,
                var v when string.Equals(v, Hijri, StringComparison.OrdinalIgnoreCase) => Hijri,
                _ => Jalali
            };
    }
}
