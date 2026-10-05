using Api_Vapp.Constants;

namespace Api_Vapp.Data
{
    /// <summary>
    /// کاتالوگ مناسبت‌های سیستمی برای ارسال تبریک/تسلیت.
    /// منابع: تعطیلات رسمی مصوب + تقویم مرکز تقویم مؤسسه ژئوفیزیک دانشگاه تهران
    /// + مناسبت‌های رایج تقویم رسمی کشور.
    ///
    /// همهٔ تاریخ‌ها شمسی (جلالی) هستند.
    /// مناسبت‌های مذهبی طبق تقویم رسمی سال ۱۴۰۵ (مرکز تقویم مؤسسه ژئوفیزیک) ذخیره شده‌اند
    /// و هر سال باید در پنل ادمین با تاریخ شمسی همان سال به‌روز شوند.
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

        private static string Congrats(string title) =>
            $"سلام {{{{نام}}}} عزیز!\n\n{title}\n\n{{{{نام شرکت}}}}";

        private static string Condolence(string title) =>
            $"با عرض تسلیت فرا رسیدن {title}.\n\n{{{{نام شرکت}}}}";

        public static IReadOnlyList<SeedItem> All { get; } =
        [
            // ═══════════════════════════════════════════
            // تعطیلات / مناسبت‌های رسمی جلالی (ثابت)
            // ═══════════════════════════════════════════
            new("NOROOZ_1", "آغاز نوروز", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 1,
                Congrats("عید نوروزتان مبارک! سالی سرشار از سلامتی و موفقیت آرزو می‌کنیم."), 10),
            new("NOROOZ_2", "عید نوروز (۲ فروردین)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 2,
                Congrats("ایام نوروز مبارک."), 11),
            new("NOROOZ_3", "عید نوروز (۳ فروردین)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 3,
                Congrats("ایام نوروز مبارک."), 12),
            new("NOROOZ_4", "عید نوروز (۴ فروردین)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 4,
                Congrats("ایام نوروز مبارک."), 13),
            new("ISLAMIC_REPUBLIC_DAY", "روز جمهوری اسلامی ایران", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 12,
                Congrats("فرارسیدن روز جمهوری اسلامی ایران را گرامی می‌داریم."), 20),
            new("SIZDAH_BEDAR", "روز طبیعت (سیزده‌بدر)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 13,
                Congrats("سیزده‌بدر و روز طبیعت مبارک! روزی خوش و پرانرژی برای شما آرزو می‌کنیم."), 21),

            new("KHOMEINI_DEATH", "رحلت امام خمینی (ره)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 3, 14,
                Condolence("سالروز رحلت امام خمینی (ره)"), 40),
            new("KHORDAD_15", "قیام ۱۵ خرداد", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 3, 15,
                Congrats("فرارسیدن سالروز قیام ۱۵ خرداد را گرامی می‌داریم."), 41),

            new("BAHMAN_22", "پیروزی انقلاب اسلامی (۲۲ بهمن)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 11, 22,
                Congrats("فرارسیدن ۲۲ بهمن، سالروز پیروزی انقلاب اسلامی را گرامی می‌داریم."), 50),
            new("OIL_NATIONALIZATION", "ملی شدن صنعت نفت", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 12, 29,
                Congrats("فرارسیدن روز ملی شدن صنعت نفت ایران را گرامی می‌داریم."), 55),

            // ═══════════════════════════════════════════
            // مناسبت‌های ملی / فرهنگی رایج (جلالی)
            // ═══════════════════════════════════════════
            new("ARMY_DAY", "روز ارتش", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 29,
                Congrats("روز ارتش جمهوری اسلامی ایران گرامی باد."), 60),
            new("WORKER_DAY", "روز کارگر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 2, 11,
                Congrats("روز کارگر مبارک."), 61),
            new("TEACHER_DAY", "روز معلم", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 2, 12,
                Congrats("روز معلم مبارک."), 62),
            new("STUDENT_DAY", "روز دانش‌آموز", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 8, 13,
                Congrats("روز دانش‌آموز مبارک."), 63),
            new("STUDENT_DAY_UNI", "روز دانشجو", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 9, 16,
                Congrats("روز دانشجو مبارک."), 64),
            new("YALDA", "شب یلدا", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 9, 30,
                Congrats("شب یلدا مبارک! طولانی‌ترین شب سال را در کنار عزیزانتان خوش بگذرانید."), 65),
            new("MEHRGAN", "جشن مهرگان", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 7, 16,
                Congrats("جشن مهرگان مبارک."), 66),
            new("SEPANDARMAZGAN", "سپندارمذگان", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 12, 5,
                Congrats("سپندارمذگان مبارک."), 67),

            // سازگاری با کدهای قبلی (اصلاح تقویم)
            new("NOROOZ", "نوروز", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 1, 1,
                Congrats("عید نوروزتان مبارک! سالی سرشار از سلامتی و موفقیت آرزو می‌کنیم."), 9),

            // ═══════════════════════════════════════════
            // مناسبت‌های مذهبی — تاریخ شمسی تقویم رسمی ۱۴۰۵
            // ═══════════════════════════════════════════
            new("TASUA", "تاسوعای حسینی", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 4, 3,
                Condolence("تاسوعای حسینی"), 100),
            new("ASHURA", "عاشورای حسینی", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 4, 4,
                Condolence("عاشورای حسینی"), 101),
            new("ARBAYEEN", "اربعین حسینی", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 5, 13,
                Condolence("اربعین حسینی"), 110),
            new("PROPHET_DEATH", "رحلت پیامبر (ص) و شهادت امام حسن (ع)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 5, 21,
                Condolence("رحلت پیامبر اکرم (ص) و شهادت امام حسن مجتبی (ع)"), 111),
            new("IMAM_REZA_DEATH", "شهادت امام رضا (ع)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 5, 22,
                Condolence("شهادت امام رضا (ع)"), 112),
            new("IMAM_ASKARI_DEATH", "شهادت امام حسن عسکری (ع)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 5, 30,
                Condolence("شهادت امام حسن عسکری (ع)"), 120),
            new("FATIMA_DEATH", "شهادت حضرت فاطمه زهرا (س)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 8, 22,
                Condolence("شهادت حضرت فاطمه زهرا (س)"), 130),
            new("FATIMIYYAH", "ایام فاطمیه", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 8, 22,
                Condolence("ایام فاطمیه"), 131),
            new("IMAM_ALI_DEATH", "شهادت امام علی (ع)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 12, 9,
                Condolence("شهادت امام علی (ع)"), 140),
            new("IMAM_SADEQ_DEATH", "شهادت امام جعفر صادق (ع)", OccasionTypeCodes.Death, OccasionCategories.Condolence, OccasionCalendarTypes.Jalali, 1, 25,
                Condolence("شهادت امام جعفر صادق (ع)"), 150),

            new("PROPHET_BIRTH", "ولادت پیامبر (ص) و امام صادق (ع)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 6, 8,
                Congrats("ولادت پیامبر اکرم (ص) و امام جعفر صادق (ع) مبارک."), 200),
            new("IMAM_ALI_BIRTH", "ولادت امام علی (ع) — روز پدر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 10, 2,
                Congrats("ولادت امام علی (ع) و روز پدر مبارک."), 210),
            new("FATHER_DAY", "روز پدر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 10, 2,
                Congrats("روز پدر مبارک."), 211),
            new("MABATH", "مبعث پیامبر (ص)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 10, 16,
                Congrats("عید مبعث پیامبر اکرم (ص) مبارک."), 220),
            new("IMAM_MAHDI_BIRTH", "ولادت امام زمان (عج) — نیمه شعبان", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 11, 4,
                Congrats("ولادت امام زمان (عج) و نیمه شعبان مبارک."), 230),
            new("EID_FITR", "عید سعید فطر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 12, 19,
                Congrats("عید سعید فطر مبارک."), 240),
            new("EID_FITR_2", "تعطیلی عید فطر (۲ شوال)", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 12, 20,
                Congrats("عید سعید فطر مبارک."), 241),
            new("EID_ADHA", "عید سعید قربان", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 3, 6,
                Congrats("عید سعید قربان مبارک."), 250),
            new("EID_GHADIR", "عید سعید غدیر خم", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 3, 14,
                Congrats("عید سعید غدیر خم مبارک."), 251),

            new("FATIMA_BIRTH", "ولادت حضرت فاطمه (س) — روز زن و مادر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 9, 9,
                Congrats("ولادت حضرت فاطمه زهرا (س) و روز زن و مادر مبارک."), 260),
            new("WOMEN_DAY", "روز زن / مادر", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 9, 9,
                Congrats("روز زن و مادر مبارک."), 261),

            new("MASUMEH_BIRTH", "ولادت حضرت معصومه (س) — روز دختران", OccasionTypeCodes.Holiday, OccasionCategories.Congratulation, OccasionCalendarTypes.Jalali, 2, 30,
                Congrats("ولادت حضرت معصومه (س) و روز دختران مبارک."), 270),
        ];
    }
}
