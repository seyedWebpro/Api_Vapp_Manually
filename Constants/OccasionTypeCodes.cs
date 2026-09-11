namespace Api_Vapp.Constants
{
    /// <summary>
    /// نوع مناسبت (سازگار با فیلد Type قبلی + نگاشت به دسته تبریک/تسلیت)
    /// </summary>
    public static class OccasionTypeCodes
    {
        public const string Holiday = "Holiday";
        public const string Death = "Death";
        public const string Custom = "Custom";

        public static readonly string[] All = [Holiday, Death, Custom];

        public static bool IsKnown(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && All.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        public static string Normalize(string? value) =>
            value?.Trim() switch
            {
                var v when string.Equals(v, Holiday, StringComparison.OrdinalIgnoreCase) => Holiday,
                var v when string.Equals(v, Death, StringComparison.OrdinalIgnoreCase) => Death,
                _ => Custom
            };

        public static string ToCategory(string type) =>
            string.Equals(Normalize(type), Death, StringComparison.Ordinal)
                ? OccasionCategories.Condolence
                : OccasionCategories.Congratulation;
    }
}
