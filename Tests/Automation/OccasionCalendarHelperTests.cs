using Api_Vapp.Constants;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Automation
{
    public class OccasionCalendarHelperTests
    {
        [Fact]
        public void IsOccasionToday_Jalali_Norooz_MatchesFarvardin1()
        {
            var pc = new System.Globalization.PersianCalendar();
            var local = pc.ToDateTime(1405, 1, 1, 12, 0, 0, 0);
            var tehran = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran");
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tehran);
            var parts = OccasionCalendarHelper.GetTodayParts(utc);

            Assert.Equal(1, parts.JalaliMonth);
            Assert.Equal(1, parts.JalaliDay);
            Assert.True(OccasionCalendarHelper.IsOccasionToday(OccasionCalendarTypes.Jalali, 1, 1, parts));
        }

        [Fact]
        public void IsOccasionToday_Gregorian_MatchesMonthDay()
        {
            var utc = DateTime.SpecifyKind(new DateTime(2026, 9, 11, 10, 0, 0), DateTimeKind.Utc);
            var parts = OccasionCalendarHelper.GetTodayParts(utc);

            Assert.True(OccasionCalendarHelper.IsOccasionToday(
                OccasionCalendarTypes.Gregorian, 9, 11, parts));
            Assert.False(OccasionCalendarHelper.IsOccasionToday(
                OccasionCalendarTypes.Gregorian, 9, 12, parts));
        }

        [Fact]
        public void HasReachedScheduledTimeTehran_Before_ReturnsFalse()
        {
            // 06:30 Tehran ≈ 03:00 UTC in late summer (UTC+3:30)
            var utc = DateTime.SpecifyKind(new DateTime(2026, 9, 11, 3, 0, 0), DateTimeKind.Utc);
            Assert.False(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(10, 0, 0), utc));
        }

        [Fact]
        public void HasReachedScheduledTimeTehran_After_ReturnsTrue()
        {
            var utc = DateTime.SpecifyKind(new DateTime(2026, 9, 11, 8, 0, 0), DateTimeKind.Utc);
            Assert.True(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(10, 0, 0), utc));
        }

        [Fact]
        public void HasReachedScheduledTimeTehran_Null_ReturnsTrue()
        {
            Assert.True(OccasionCalendarHelper.HasReachedScheduledTimeTehran(null, DateTime.UtcNow));
        }

        [Fact]
        public void CalculateDaysRemaining_Today_ReturnsZero()
        {
            var parts = OccasionCalendarHelper.GetTodayParts();
            var days = OccasionCalendarHelper.CalculateDaysRemaining(
                OccasionCalendarTypes.Jalali, parts.JalaliMonth, parts.JalaliDay);
            Assert.Equal(0, days);
        }

        [Fact]
        public void OccasionMessagePersonalizer_ReplacesBusinessAndOccasion()
        {
            var text = OccasionMessagePersonalizer.Apply(
                "سلام {{نام}} از {{نام شرکت}} — {{مناسبت}}",
                "علی",
                "فروشگاه تست",
                "نوروز");

            Assert.Contains("علی", text);
            Assert.Contains("فروشگاه تست", text);
            Assert.Contains("نوروز", text);
        }

        [Fact]
        public void HasReachedScheduledTimeTehran_ExactMinute_ReturnsTrue()
        {
            var tehran = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran");
            // بساز ۱۰:۰۰:۰۰ تهران در روز مشخص
            var local = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Unspecified);
            var utcExact = TimeZoneInfo.ConvertTimeToUtc(local, tehran);
            Assert.True(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(10, 0, 0), utcExact));

            var utcBefore = utcExact.AddSeconds(-1);
            Assert.False(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(10, 0, 0), utcBefore));
        }

        [Fact]
        public void HasReachedScheduledTimeTehran_Afternoon_Uses24HourClock()
        {
            var tehran = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran");
            var local = new DateTime(2026, 9, 11, 14, 30, 0, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, tehran);

            Assert.False(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(15, 0, 0), utc));
            Assert.True(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(14, 30, 0), utc));
            Assert.True(OccasionCalendarHelper.HasReachedScheduledTimeTehran(new TimeSpan(14, 0, 0), utc));
        }

        [Fact]
        public void GetTehranDayUtcRange_CoversFullTehranDay()
        {
            var range = OccasionCalendarHelper.GetTehranDayUtcRange(new DateOnly(2026, 9, 11));
            Assert.True(range.EndUtc > range.StartUtc);
            Assert.Equal(TimeSpan.FromDays(1), range.EndUtc - range.StartUtc);

            var mid = range.StartUtc.AddHours(12);
            var parts = OccasionCalendarHelper.GetTodayParts(mid);
            Assert.Equal(new DateOnly(2026, 9, 11), parts.TehranDate);
        }

        [Fact]
        public void OccasionCategories_Normalize_DeathMapsToCondolence()
        {
            Assert.Equal(OccasionCategories.Condolence, OccasionTypeCodes.ToCategory(OccasionTypeCodes.Death));
            Assert.Equal(OccasionCategories.Congratulation, OccasionTypeCodes.ToCategory(OccasionTypeCodes.Holiday));
        }
    }
}
