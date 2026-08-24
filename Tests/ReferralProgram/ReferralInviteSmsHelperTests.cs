using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.ReferralProgram;

public class ReferralInviteSmsHelperTests
{
    [Fact]
    public void NormalizeClosingText_Empty_ReturnsDefault()
    {
        Assert.Equal(
            ReferralInviteSmsHelper.DefaultClosingText,
            ReferralInviteSmsHelper.NormalizeClosingText("   "));
    }

    [Fact]
    public void IsCustomClosingText_Default_ReturnsFalse()
    {
        Assert.False(ReferralInviteSmsHelper.IsCustomClosingText(
            ReferralInviteSmsHelper.DefaultClosingText));
    }

    [Fact]
    public void IsCustomClosingText_Custom_ReturnsTrue()
    {
        Assert.True(ReferralInviteSmsHelper.IsCustomClosingText(
            "کد را بدهید تا با خرید از کلینیک زیبایی پاداش فعال شود."));
    }

    [Fact]
    public void TryValidateClosingText_TooLong_ReturnsFalse()
    {
        var tooLong = new string('ا', ReferralInviteSmsHelper.ClosingTextMaxLength + 1);
        Assert.False(ReferralInviteSmsHelper.TryValidateClosingText(tooLong, out var error));
        Assert.Contains("حداکثر", error);
    }

    [Fact]
    public void BuildInviteMessage_DoesNotContainStoreWord()
    {
        var message = ReferralInviteSmsHelper.BuildInviteMessage(
            "پاداش تست",
            "REF111111",
            "FixedAmount",
            true,
            10000,
            true,
            50000,
            null);

        Assert.DoesNotContain("فروشگاه", message);
        Assert.Contains(ReferralInviteSmsHelper.DefaultClosingText, message);
        Assert.Contains("REF111111", message);
    }

    [Fact]
    public void BuildReferrerRewardMessage_DoesNotContainStoreWord()
    {
        var message = ReferralInviteSmsHelper.BuildReferrerRewardMessage("پاداش تست", 50000);
        Assert.DoesNotContain("فروشگاه", message);
        Assert.Contains(ReferralInviteSmsHelper.DefaultReferrerRewardClosingText, message);
    }
}
