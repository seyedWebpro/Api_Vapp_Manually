using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

public class BookingWindowHelperTests
{
    [Theory]
    [InlineData(null, 7, 7)]
    [InlineData(30, 7, 30)]
    [InlineData(0, 14, 14)]
    [InlineData(500, 14, 14)]
    public void ResolveEffectiveDays_ReturnsExpected(int? configured, int global, int expected)
    {
        var actual = BookingWindowHelper.ResolveEffectiveDays(configured, global);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsWithinWindow_RespectsInclusiveEndDate()
    {
        var days = 7;
        var start = BookingWindowHelper.GetStartDateUtc();
        var end = BookingWindowHelper.GetEndDateUtc(days);

        Assert.True(BookingWindowHelper.IsWithinWindow(start, days));
        Assert.True(BookingWindowHelper.IsWithinWindow(end, days));
        Assert.False(BookingWindowHelper.IsWithinWindow(end.AddDays(1), days));
    }
}
