namespace Api_Vapp.Constants
{
    public static class BookingActivityTypes
    {
        public const string BeautySalon = "beauty_salon";
        public const string Medical = "medical";
        public const string Consulting = "consulting";
        public const string Fitness = "fitness";
        public const string Education = "education";
        public const string VipServices = "vip_services";
        public const string Other = "other";

        public static readonly IReadOnlyList<(string Code, string Title)> Catalog = new List<(string, string)>
        {
            (BeautySalon, "سالن زیبایی"),
            (Medical, "پزشکی و درمان"),
            (Consulting, "مشاوره"),
            (Fitness, "ورزش و تناسب اندام"),
            (Education, "آموزش"),
            (VipServices, "خدمات VIP"),
            (Other, "سایر")
        };

        public static bool IsValid(string? code)
        {
            return !string.IsNullOrWhiteSpace(code) &&
                   Catalog.Any(c => c.Code == code);
        }

        public static string? GetTitle(string code)
        {
            return Catalog.FirstOrDefault(c => c.Code == code).Title;
        }
    }
}
