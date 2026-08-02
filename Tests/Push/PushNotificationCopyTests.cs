using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Push;

public class PushNotificationCopyTests
{
    [Fact]
    public void WalletCredited_ContainsAmountAndBalance()
    {
        var (title, body) = PushNotificationCopy.WalletCredited(50_000, 120_000);
        Assert.Equal("شارژ کیف پول", title);
        Assert.Contains("50", body);
        Assert.Contains("120", body);
        Assert.Contains("تومان", body);
    }

    [Fact]
    public void AppUpdate_UsesVersionAndOptionalNotes()
    {
        var (title, body) = PushNotificationCopy.AppUpdate("2.5.0", "رفع چند باگ مهم");
        Assert.Equal("به‌روزرسانی وپ", title);
        Assert.Contains("2.5.0", body);
        Assert.Contains("رفع چند باگ مهم", body);
    }

    [Fact]
    public void CampaignCompleted_IncludesCounts()
    {
        var (title, body) = PushNotificationCopy.CampaignCompleted("عیدانه", 10, 2);
        Assert.Equal("نتیجه کمپین", title);
        Assert.Contains("10", body);
        Assert.Contains("2", body);
        Assert.Contains("عیدانه", body);
    }

    [Fact]
    public void FinancialDailyReport_IsReadablePersian()
    {
        var (title, body) = PushNotificationCopy.FinancialDailyReport(1_000_000, 200_000, 50_000, 3);
        Assert.Equal("خلاصه مالی روزانه", title);
        Assert.Contains("موجودی", body);
        Assert.Contains("3", body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AccountStatusChanged_VariesByActiveFlag(bool isActive)
    {
        var (title, body) = PushNotificationCopy.AccountStatusChanged(isActive);
        Assert.False(string.IsNullOrWhiteSpace(title));
        Assert.False(string.IsNullOrWhiteSpace(body));
        if (isActive)
            Assert.Contains("فعال", title);
        else
            Assert.Contains("غیرفعال", title);
    }
}
