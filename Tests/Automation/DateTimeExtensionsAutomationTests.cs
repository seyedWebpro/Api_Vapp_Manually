using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Automation
{
    public class DateTimeExtensionsAutomationTests
    {
        [Fact]
        public void IsBirthdayToday_MatchesMonthAndDay_IgnoresYear()
        {
            var dob = new DateTime(1990, 8, 13, 0, 0, 0, DateTimeKind.Utc);
            var today = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
            Assert.True(dob.IsBirthdayToday(today));
        }

        [Fact]
        public void IsBirthdayToday_Feb29_InNonLeapYear_MatchesFeb28()
        {
            var dob = new DateTime(2000, 2, 29, 0, 0, 0, DateTimeKind.Utc);
            var today = new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc); // non-leap
            Assert.False(DateTime.IsLeapYear(2025));
            Assert.True(dob.IsBirthdayToday(today));
        }

        [Fact]
        public void IsBirthdayToday_Feb29_InLeapYear_MatchesFeb29()
        {
            var dob = new DateTime(2000, 2, 29, 0, 0, 0, DateTimeKind.Utc);
            var today = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
            Assert.True(DateTime.IsLeapYear(2024));
            Assert.True(dob.IsBirthdayToday(today));
        }

        [Fact]
        public void HasReachedScheduledTime_Before_ReturnsFalse()
        {
            var today = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
            var now = today.AddHours(8).AddMinutes(30);
            Assert.False(((TimeSpan?)TimeSpan.FromHours(9)).HasReachedScheduledTime(today, now));
        }

        [Fact]
        public void HasReachedScheduledTime_After_ReturnsTrue_CatchUp()
        {
            var today = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
            var now = today.AddHours(9).AddMinutes(15); // 15 min late — catch-up
            Assert.True(((TimeSpan?)TimeSpan.FromHours(9)).HasReachedScheduledTime(today, now));
        }

        [Fact]
        public void HasReachedScheduledTime_Null_ReturnsTrue()
        {
            var today = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
            Assert.True(((TimeSpan?)null).HasReachedScheduledTime(today, today.AddHours(1)));
        }

        [Fact]
        public void EnsureDateOnlyUtc_StripsTime()
        {
            var dt = new DateTime(2026, 8, 13, 20, 30, 0, DateTimeKind.Utc);
            var result = dt.EnsureDateOnlyUtc();
            Assert.Equal(new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), result);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Theory]
        [InlineData("2026-08-13", 2026, 8, 13)]
        [InlineData("2026/08/13", 2026, 8, 13)]
        public void FlexibleDateTime_DateOnly_IsUtcMidnightSameCalendarDay(string input, int y, int m, int d)
        {
            var parsed = FlexibleDateTimeConverter.Parse(input);
            Assert.NotNull(parsed);
            Assert.Equal(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc), parsed!.Value);
            Assert.Equal(DateTimeKind.Utc, parsed.Value.Kind);
        }

        [Fact]
        public void FlexibleDateTime_IsoZMidnight_IsUtcDate()
        {
            var parsed = FlexibleDateTimeConverter.Parse("2026-08-13T00:00:00Z");
            Assert.NotNull(parsed);
            Assert.Equal(new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), parsed!.Value.Date);
        }

        [Fact]
        public void SafeDateInYear_Feb29_NonLeap_BecomesFeb28()
        {
            var d = DateTimeExtensions.SafeDateInYear(2025, 2, 29);
            Assert.Equal(new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc), d);
        }
    }
}
