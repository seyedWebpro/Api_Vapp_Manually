using Api_Vapp.Constants;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.SmsDelivery;

public class SmsDeliveryRefundEligibilityTests
{
    [Theory]
    [InlineData(3, true)]   // NotDelivered
    [InlineData(12, true)]
    [InlineData(21, true)]
    [InlineData(28, true)]
    [InlineData(4, true)]   // Rejected
    [InlineData(17, true)]
    [InlineData(7, true)]   // SendFailed
    [InlineData(2, false)]  // DeliveredToPhone
    [InlineData(0, false)]  // SentToOperator
    [InlineData(1, false)]
    [InlineData(22, false)] // PendingApproval
    public void MapAndRefundEligibility_MatchesExpected(int statusCode, bool shouldRefund)
    {
        var category = SmsDeliveryStatusMapper.MapToCategory(statusCode);
        Assert.Equal(shouldRefund, SmsDeliveryStatusMapper.IsRefundEligibleCategory(category));
        Assert.Equal(shouldRefund, SmsDeliveryCategories.IsWalletRefundEligible(category));
    }

    [Fact]
    public void FinalNotDelivered_IsFinalAndRefundable()
    {
        Assert.True(SmsDeliveryStatusMapper.IsFinalStatus(3));
        Assert.True(SmsDeliveryStatusMapper.IsRefundEligibleCategory(SmsDeliveryCategories.NotDelivered));
    }

    [Fact]
    public void DeliveredToPhone_IsFinalButNotRefundable()
    {
        Assert.True(SmsDeliveryStatusMapper.IsFinalStatus(2));
        Assert.False(SmsDeliveryStatusMapper.IsRefundEligibleCategory(SmsDeliveryCategories.DeliveredToPhone));
    }
}
