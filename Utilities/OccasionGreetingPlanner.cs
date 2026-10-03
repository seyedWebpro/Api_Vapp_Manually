using Api_Vapp.Constants;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// تصمیم‌گیری ارسال تبریک/تسلیت مناسبتی مستقیم از جدول مناسبت‌های کاربر
    /// (بدون وابستگی به AutomatedMessage). منطق خالص و بدون دیتابیس برای تست‌پذیری.
    /// </summary>
    public static class OccasionGreetingPlanner
    {
        /// <summary>ساعت ارسال پیش‌فرض تهران وقتی کاربر پروفایل یا ساعت ندارد</summary>
        public static readonly TimeSpan DefaultSendTimeTehran = new(10, 0, 0);

        public sealed record EnabledOccasion(int UserId, SpecialOccasion Occasion, UserOccasionPreference? Preference);

        public sealed record PlannedBatch(
            int UserId,
            SpecialOccasion Occasion,
            string Content,
            IReadOnlyList<Contact> Contacts);

        public sealed record SkippedOccasion(int UserId, int OccasionId, string Reason);

        public const string SkipReasonCategoryDisabled = "CategoryDisabled";
        public const string SkipReasonEmptyTemplate = "EmptyTemplate";
        public const string SkipReasonMissingBusinessName = "MissingBusinessName";
        public const string SkipReasonNoRecipients = "NoRecipients";

        /// <summary>
        /// جفت‌های (کاربر، مناسبت) فعال امروز:
        /// Preference فعال روی مناسبت سیستمی یا مناسبت سفارشی خود کاربر،
        /// به‌علاوه مناسبت سفارشی قدیمی بدون Preference (سازگاری با رفتار قبلی).
        /// </summary>
        public static List<EnabledOccasion> ResolveEnabledOccasions(
            IReadOnlyCollection<SpecialOccasion> occasionsToday,
            IReadOnlyCollection<UserOccasionPreference> preferences)
        {
            var occasionById = occasionsToday.ToDictionary(o => o.Id);
            var activePreferences = preferences
                .Where(p => !p.IsDeleted && occasionById.ContainsKey(p.SpecialOccasionId))
                .ToList();

            var result = new List<EnabledOccasion>();

            foreach (var preference in activePreferences)
            {
                var occasion = occasionById[preference.SpecialOccasionId];
                if (!occasion.IsSystem && occasion.UserId != preference.UserId)
                    continue;

                if (OccasionMessagePersonalizer.IsEnabledForUser(occasion, preference))
                    result.Add(new EnabledOccasion(preference.UserId, occasion, preference));
            }

            var ownersWithPreference = activePreferences
                .Select(p => (p.UserId, p.SpecialOccasionId))
                .ToHashSet();

            foreach (var occasion in occasionsToday.Where(o => !o.IsSystem && o.UserId.HasValue))
            {
                if (!ownersWithPreference.Contains((occasion.UserId!.Value, occasion.Id))
                    && OccasionMessagePersonalizer.IsEnabledForUser(occasion, null))
                {
                    result.Add(new EnabledOccasion(occasion.UserId.Value, occasion, null));
                }
            }

            return result;
        }

        public static TimeSpan ResolveSendTimeTehran(UserOccasionProfile? profile) =>
            profile?.ScheduledTimeTehran ?? DefaultSendTimeTehran;

        public static bool IsCategoryEnabled(SpecialOccasion occasion, UserOccasionProfile? profile)
        {
            if (profile == null)
                return true;

            return OccasionCategories.Normalize(occasion.Category) == OccasionCategories.Condolence
                ? profile.CondolencesEnabled
                : profile.CongratulationsEnabled;
        }

        /// <summary>
        /// ساخت دسته‌های ارسال یک کاربر. فرض: زمان ارسال کاربر فرا رسیده است.
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="userOccasions">مناسبت‌های فعال کاندید برای همین کاربر</param>
        /// <param name="profile">پروفایل مناسبتی کاربر (اختیاری)</param>
        /// <param name="userContacts">مخاطبین کاربر</param>
        /// <param name="handledKeys">جفت‌های (مناسبت، مخاطب) که امروز تهران قبلاً صف/ارسال شده‌اند</param>
        public static (List<PlannedBatch> Batches, List<SkippedOccasion> Skipped) PlanUserBatches(
            int userId,
            IReadOnlyCollection<EnabledOccasion> userOccasions,
            UserOccasionProfile? profile,
            IReadOnlyCollection<Contact> userContacts,
            IReadOnlySet<(int OccasionId, int ContactId)> handledKeys)
        {
            var batches = new List<PlannedBatch>();
            var skipped = new List<SkippedOccasion>();

            foreach (var item in userOccasions.Where(o => o.UserId == userId))
            {
                var occasion = item.Occasion;

                if (!IsCategoryEnabled(occasion, profile))
                {
                    skipped.Add(new SkippedOccasion(userId, occasion.Id, SkipReasonCategoryDisabled));
                    continue;
                }

                var template = OccasionMessagePersonalizer.ResolveEffectiveTemplate(occasion, item.Preference);
                if (string.IsNullOrWhiteSpace(template))
                {
                    skipped.Add(new SkippedOccasion(userId, occasion.Id, SkipReasonEmptyTemplate));
                    continue;
                }

                // قالب‌های سیستمی تقریباً همیشه {{نام شرکت}} دارند؛ بدون BusinessName پیام ناقص می‌رود
                if (OccasionMessagePersonalizer.NeedsBusinessName(template)
                    && !OccasionMessagePersonalizer.HasBusinessName(profile?.BusinessName))
                {
                    skipped.Add(new SkippedOccasion(userId, occasion.Id, SkipReasonMissingBusinessName));
                    continue;
                }

                var contacts = userContacts
                    .Where(c => !string.IsNullOrWhiteSpace(c.MobileNumber))
                    .Where(c => OccasionAudienceHelper.IsContactInAudience(c, item.Preference))
                    .Where(c => !handledKeys.Contains((occasion.Id, c.Id)))
                    .ToList();

                if (contacts.Count == 0)
                {
                    skipped.Add(new SkippedOccasion(userId, occasion.Id, SkipReasonNoRecipients));
                    continue;
                }

                var content = OccasionMessagePersonalizer.ApplyForQueuePreview(
                    template,
                    profile?.BusinessName,
                    occasion.Name);

                batches.Add(new PlannedBatch(userId, occasion, content, contacts));
            }

            return (batches, skipped);
        }
    }
}
