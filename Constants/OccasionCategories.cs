namespace Api_Vapp.Constants
{
    /// <summary>
    /// دسته‌بندی مناسبت‌ها برای تبریک / تسلیت
    /// </summary>
    public static class OccasionCategories
    {
        public const string Congratulation = "Congratulation";
        public const string Condolence = "Condolence";

        public static readonly string[] All = [Congratulation, Condolence];

        public static bool IsKnown(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && All.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        public static string Normalize(string? value) =>
            value?.Trim() switch
            {
                var v when string.Equals(v, Condolence, StringComparison.OrdinalIgnoreCase) => Condolence,
                _ => Congratulation
            };

        public static string ToPersian(string category) =>
            string.Equals(category, Condolence, StringComparison.OrdinalIgnoreCase)
                ? "تسلیت"
                : "تبریک";
    }
}
