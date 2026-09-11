using Api_Vapp.Constants;

namespace Api_Vapp.Data
{
    /// <summary>
    /// کاتالوگ مناسبت‌های سیستمی سالانه (جلالی ثابت + قمری برای عزاداری‌ها)
    /// </summary>
    public static class SystemOccasionCatalog
    {
        public sealed record SeedItem(
            string Code,
            string Name,
            string Type,
            string Category,
            string CalendarType,
            byte Month,
            byte Day,
            string DefaultMessage,
            int SortOrder);

        public static IReadOnlyList<SeedItem> All { get; } =
        [
            // —— تبریک‌های جلالی ثابت
            new("NOROOZ", "نوروز", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 1,
                "سلام {{نام}} عزیز!\n\nنوروزتان پیروز و سالی سرشار از سلامتی و موفقیت مبارک.\n\n{{نام شرکت}}", 10),
            new("SIZDAH_BEDAR", "سیزده‌بدر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 13,
                "سلام {{نام}} عزیز!\n\nسیزده‌بدر مبارک! روزی خوش و پرانرژی برای شما آرزو می‌کنیم.\n\n{{نام شرکت}}", 20),
            new("MEHRGAN", "جشن مهرگان", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 7, 16,
                "سلام {{نام}} عزیز!\n\nجشن مهرگان مبارک.\n\n{{نام شرکت}}", 30),
            new("YALDA", "شب یلدا", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 10, 1,
                "سلام {{نام}} عزیز!\n\nشب یلدا مبارک! طولانی‌ترین شب سال را در کنار عزیزانتان خوش بگذرانید.\n\n{{نام شرکت}}", 40),
            new("BAHMAN_22", "۲۲ بهمن", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 11, 22,
                "سلام {{نام}} عزیز!\n\nفرارسیدن ۲۲ بهمن را گرامی می‌داریم.\n\n{{نام شرکت}}", 50),
            new("WORKER_DAY", "روز کارگر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 2, 11,
                "سلام {{نام}} عزیز!\n\nروز کارگر مبارک.\n\n{{نام شرکت}}", 60),
            new("TEACHER_DAY", "روز معلم", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 2, 12,
                "سلام {{نام}} عزیز!\n\nروز معلم مبارک.\n\n{{نام شرکت}}", 70),
            new("WOMEN_DAY", "روز زن / مادر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 8, 20,
                "سلام {{نام}} عزیز!\n\nروز زن و مادر مبارک.\n\n{{نام شرکت}}", 80),
            new("FATHER_DAY", "روز پدر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 11, 13,
                "سلام {{نام}} عزیز!\n\nروز پدر مبارک.\n\n{{نام شرکت}}", 90),

            // —— تسلیت‌های قمری (ماه/روز هجری قمری)
            new("TASUA", "تاسوعا", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Hijri, 1, 9,
                "با عرض تسلیت فرا رسیدن تاسوعای حسینی.\n\n{{نام شرکت}}", 200),
            new("ASHURA", "عاشورا", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Hijri, 1, 10,
                "با عرض تسلیت فرا رسیدن عاشورای حسینی.\n\n{{نام شرکت}}", 210),
            new("ARBAYEEN", "اربعین حسینی", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Hijri, 2, 20,
                "با عرض تسلیت فرا رسیدن اربعین حسینی.\n\n{{نام شرکت}}", 220),
            new("FATIMIYYAH", "ایام فاطمیه", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Hijri, 6, 3,
                "با عرض تسلیت ایام فاطمیه.\n\n{{نام شرکت}}", 230),
            new("PROPHET_DEATH", "رحلت پیامبر (ص)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Hijri, 3, 28,
                "با عرض تسلیت سالروز رحلت پیامبر اکرم (ص).\n\n{{نام شرکت}}", 240),
        ];
    }
}
