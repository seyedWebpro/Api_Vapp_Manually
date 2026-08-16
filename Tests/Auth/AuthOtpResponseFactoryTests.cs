using Api_Vapp.DTOs.Common;
using Api_Vapp.Services;
using Xunit;

namespace Api_Vapp.Tests.Auth;

public class AuthOtpResponseFactoryTests
{
    [Theory]
    [InlineData(45, "45 ثانیه")]
    [InlineData(120, "2 دقیقه")]
    [InlineData(125, "2 دقیقه و 5 ثانیه")]
    public void FormatRetryWaitMessage_IsHumanFriendly(int seconds, string durationHint)
    {
        var msg = AuthOtpResponseFactory.FormatRetryWaitMessage(seconds);
        Assert.Contains("کد تایید اخیراً ارسال شده است", msg);
        Assert.Contains(durationHint, msg);
    }

    [Fact]
    public void RateLimited_HasErrorCode_AndRetryAfter()
    {
        var dto = AuthOtpResponseFactory.RateLimited(90);
        Assert.False(dto.Success);
        Assert.Equal(429, dto.StatusCode);
        Assert.Equal(ErrorCodes.OtpRateLimited, dto.ErrorCode);
        Assert.Equal(90, dto.RetryAfterSeconds);
        Assert.Contains("صبر کنید", dto.Message);
    }

    [Fact]
    public void Success_IncludesRetryAfter_ForMobileCountdown()
    {
        var dto = AuthOtpResponseFactory.Success(
            "کد تایید جدید به شماره موبایل شما ارسال شد",
            otpCode: "1234",
            expiresInSeconds: 300,
            retryAfterSeconds: 120);

        Assert.True(dto.Success);
        Assert.Equal(200, dto.StatusCode);
        Assert.Equal(300, dto.ExpiresInSeconds);
        Assert.Equal(120, dto.RetryAfterSeconds);
        Assert.Null(dto.ErrorCode);
        Assert.Equal("کد تایید جدید به شماره موبایل شما ارسال شد", dto.Message);
        // TODO(remove-before-production) REMOVE_DEV_OTP
        Assert.Equal("1234", dto.OtpCode);
    }

    [Fact]
    public void NotFound_HasErrorCode()
    {
        var dto = AuthOtpResponseFactory.NotFound("کاربری یافت نشد");
        Assert.Equal(404, dto.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, dto.ErrorCode);
        Assert.False(dto.Success);
    }

    [Fact]
    public void SmsFailed_HasErrorCode()
    {
        var dto = AuthOtpResponseFactory.SmsFailed();
        Assert.Equal(503, dto.StatusCode);
        Assert.Equal(ErrorCodes.SmsFailed, dto.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(dto.Message));
    }
}
