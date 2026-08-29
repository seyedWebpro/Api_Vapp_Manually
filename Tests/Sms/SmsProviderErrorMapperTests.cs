using Api_Vapp.DTOs.Common;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Sms;

public class SmsProviderErrorMapperTests
{
    [Fact]
    public void Map_StatusMinus15_IsDuplicateNonRetryable()
    {
        var mapped = SmsProviderErrorMapper.Map(-15, "مجاز به ارسال پیام تکراری به یک شماره در کمتر از یک دقیقه نمی باشید");

        Assert.Equal(ErrorCodes.SmsDuplicate, mapped.ErrorCode);
        Assert.Equal(ControlledErrorHelper.SmsDuplicateTooSoon, mapped.UserMessage);
        Assert.True(mapped.IsNonRetryable);
        Assert.DoesNotContain("نمی باشید", mapped.UserMessage);
        Assert.Contains("یک دقیقه", mapped.UserMessage);
    }

    [Fact]
    public void Map_DuplicateKeyword_IsDuplicate()
    {
        var mapped = SmsProviderErrorMapper.Map(0, "پیام تکراری");
        Assert.Equal(ErrorCodes.SmsDuplicate, mapped.ErrorCode);
        Assert.True(mapped.IsNonRetryable);
    }

    [Fact]
    public void Map_Blacklist_IsNonRetryable()
    {
        var mapped = SmsProviderErrorMapper.Map(-10, "مشترک در لیست سیاه");
        Assert.Equal(ErrorCodes.SmsBlacklisted, mapped.ErrorCode);
        Assert.True(mapped.IsNonRetryable);
    }

    [Fact]
    public void Map_InvalidNumberKeyword_IsNonRetryable()
    {
        var mapped = SmsProviderErrorMapper.Map(0, "شماره نامعتبر است");
        Assert.Equal(ErrorCodes.SmsInvalidNumber, mapped.ErrorCode);
        Assert.True(mapped.IsNonRetryable);
    }

    [Fact]
    public void Map_UnknownNegativeStatus_IsGenericNonRetryable()
    {
        var mapped = SmsProviderErrorMapper.Map(-99, "خطای ناشناخته پنل");
        Assert.Equal(ErrorCodes.SmsFailed, mapped.ErrorCode);
        Assert.Equal(ControlledErrorHelper.SmsFailed, mapped.UserMessage);
        Assert.True(mapped.IsNonRetryable);
        Assert.DoesNotContain("ناشناخته", mapped.UserMessage);
    }

    [Fact]
    public void Map_UnknownZeroStatus_IsRetryableGeneric()
    {
        var mapped = SmsProviderErrorMapper.Map(0, "something weird");
        Assert.Equal(ErrorCodes.SmsFailed, mapped.ErrorCode);
        Assert.False(mapped.IsNonRetryable);
    }

    [Fact]
    public void MapException_DnsFailure_IsTransient()
    {
        var ex = new HttpRequestException("Name or service not known (irannovinsms.ir:443)");
        var mapped = SmsProviderErrorMapper.MapException(ex);

        Assert.Equal(ErrorCodes.SmsTemporarilyUnavailable, mapped.ErrorCode);
        Assert.False(mapped.IsNonRetryable);
        Assert.Equal(ControlledErrorHelper.SmsTemporarilyUnavailable, mapped.UserMessage);
    }

    [Fact]
    public void ControlledMessages_AreSafePersian_AndNotVagueOldText()
    {
        Assert.True(ControlledErrorHelper.IsSafeUserMessage(ControlledErrorHelper.SmsDuplicateTooSoon));
        Assert.True(ControlledErrorHelper.IsSafeUserMessage(ControlledErrorHelper.SmsFailed));
        Assert.DoesNotContain("مشکلی در ارسال پیامک پیش آمد", ControlledErrorHelper.SmsFailed);
        Assert.DoesNotContain("کاربرگرامی خطای سرور اتفاق است", ControlledErrorHelper.Unexpected);
    }
}
