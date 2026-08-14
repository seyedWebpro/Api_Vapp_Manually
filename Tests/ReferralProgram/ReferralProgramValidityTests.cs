using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.ReferralProgram;

public class ReferralProgramValidityTests
{
    private static readonly TimeZoneInfo Tehran = ResolveTehran();

    [Fact]
    public void UtcMidnightToday_IsActiveDuringIranEarlyMorning()
    {
        var start = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 13, 23, 30, 0, DateTimeKind.Utc); // 03:00 تهران

        var state = ReferralProgramValidity.Evaluate(true, start, null, "پاداش نوروز", now);

        Assert.True(state.IsValid);
        Assert.False(state.IsNotStarted);
        Assert.Contains("معتبر است", state.StatusMessage);
    }

    [Fact]
    public void FutureStartDate_ReturnsNotStartedWithPersianDate()
    {
        var start = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 14, 2, 54, 0, DateTimeKind.Utc);

        var state = ReferralProgramValidity.Evaluate(true, start, null, "پاداش نوروز", now);

        Assert.False(state.IsValid);
        Assert.True(state.IsNotStarted);
        Assert.False(state.IsExpired);
        Assert.Contains("هنوز شروع نشده", state.InvalidReason);
        Assert.Contains("۱۴۰۵/۰۵/۲۴", ToEasternArabicDigits(state.InvalidReason!));
        Assert.Contains("پاداش نوروز", state.InvalidReason);
        Assert.Contains("قابل استفاده خواهد بود", state.InvalidReason);
    }

    [Fact]
    public void IranMidnightConvertedToUtc_IsActiveImmediately()
    {
        var iranMidnight = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(iranMidnight, Tehran);
        var now = startUtc.AddMinutes(1);

        var state = ReferralProgramValidity.Evaluate(true, startUtc, null, "تست", now);

        Assert.True(state.IsValid);
        Assert.False(state.IsNotStarted);
    }

    [Fact]
    public void BeforeIranStartOfDay_IsNotStarted()
    {
        var start = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 13, 20, 0, 0, DateTimeKind.Utc); // 23:30 تهران ۱۳ مرداد

        var state = ReferralProgramValidity.Evaluate(true, start, null, "تست", now);

        Assert.False(state.IsValid);
        Assert.True(state.IsNotStarted);
    }

    [Fact]
    public void EndDateUtcMidnight_RemainsValidUntilEndOfIranDay()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 14, 16, 0, 0, DateTimeKind.Utc); // ۲۰:۳۰ تهران

        var state = ReferralProgramValidity.Evaluate(true, start, end, "تست", now);

        Assert.True(state.IsValid);
        Assert.False(state.IsExpired);
    }

    [Fact]
    public void AfterEndOfIranDay_ReturnsExpiredWithPersianDate()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 14, 20, 31, 0, DateTimeKind.Utc);

        var state = ReferralProgramValidity.Evaluate(true, start, end, "پاداش تابستان", now);

        Assert.False(state.IsValid);
        Assert.True(state.IsExpired);
        Assert.Contains("به پایان رسیده", state.InvalidReason);
        Assert.Contains("پاداش تابستان", state.InvalidReason);
        Assert.Contains("۱۴۰۵/۰۵/۲۳", ToEasternArabicDigits(state.InvalidReason!));
    }

    [Fact]
    public void InactiveProgram_ReturnsInactiveMessageEvenIfDatesAreValid()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

        var state = ReferralProgramValidity.Evaluate(false, start, null, "پاداش نوروز", now);

        Assert.False(state.IsValid);
        Assert.False(state.IsExpired);
        Assert.False(state.IsNotStarted);
        Assert.Contains("غیرفعال", state.InvalidReason);
        Assert.Contains("پاداش نوروز", state.InvalidReason);
        Assert.Contains("فعال کنید", state.InvalidReason);
    }

    [Fact]
    public void ValidOpenEndedProgram_MentionsStartDateAndNoEnd()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

        var state = ReferralProgramValidity.Evaluate(true, start, null, "پاداش نوروز", now);

        Assert.True(state.IsValid);
        Assert.Contains("معتبر است", state.StatusMessage);
        Assert.Contains("بدون تاریخ پایان", state.StatusMessage);
        Assert.Null(state.InvalidReason);
    }

    [Theory]
    [InlineData("REF123456")]
    [InlineData("ref123456")]
    public void CodeNotFoundMessage_IsSpecific(string _)
    {
        Assert.Contains("یافت نشد", ReferralProgramValidity.CodeNotFoundMessage);
        Assert.Contains("REF123456", ReferralProgramValidity.CodeNotFoundMessage);
    }

    [Fact]
    public void PublicCodeMessage_ExplainsPersonalCodeIsRequired()
    {
        Assert.Contains("شناسه برنامه", ReferralProgramValidity.PublicCodeUsedMessage);
        Assert.Contains("کد شخصی", ReferralProgramValidity.PublicCodeUsedMessage);
    }

    private static string ToEasternArabicDigits(string value) => value
        .Replace('0', '۰')
        .Replace('1', '۱')
        .Replace('2', '۲')
        .Replace('3', '۳')
        .Replace('4', '۴')
        .Replace('5', '۵')
        .Replace('6', '۶')
        .Replace('7', '۷')
        .Replace('8', '۸')
        .Replace('9', '۹');

    private static TimeZoneInfo ResolveTehran()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        }
    }
}
