using Api_Vapp.Services;
using Xunit;

namespace Api_Vapp.Tests.SmsPricing;

public class OtpSmsMessageBuilderTests
{
    [Fact]
    public void BuildForSend_IncludesDomainBoundCode_AndOptOut()
    {
        var message = OtpSmsMessageBuilder.BuildForSend(
            "1234",
            "VerifyOtp",
            autofillDomain: "ok-sms.ir");

        Assert.Equal(
            "کد تایید شما: 1234\n\n@ok-sms.ir #1234\nلغو11",
            message);
    }

    [Fact]
    public void BuildForSend_AppendsAndroidHash_AsLastLine()
    {
        var message = OtpSmsMessageBuilder.BuildForSend(
            "5678",
            "VerifyOtp",
            autofillDomain: "https://ok-sms.ir/form",
            androidAppHash: "AbCdEfGhIjK");

        Assert.Equal(
            "کد تایید شما: 5678\n\n@ok-sms.ir #5678\nلغو11\nAbCdEfGhIjK",
            message);

        var lines = message.Split('\n');
        Assert.Equal("AbCdEfGhIjK", lines[^1]);
        Assert.Equal("لغو11", lines[^2]);
    }

    [Theory]
    [InlineData("Register", "کد تایید ثبت نام: 1111")]
    [InlineData("ForgotPassword", "کد بازیابی رمز عبور: 1111")]
    public void BuildBody_UsesTemplateHeadline(string template, string expectedStart)
    {
        var body = OtpSmsMessageBuilder.BuildBody("1111", template);
        Assert.Equal(expectedStart, body);
    }

    [Fact]
    public void NormalizeOtpDigits_ConvertsPersianDigits()
    {
        Assert.Equal("4321", OtpSmsMessageBuilder.NormalizeOtpDigits("۴۳۲۱"));
    }

    [Fact]
    public void PrepareForSend_DoesNotDuplicateOptOut_WhenAndroidHashPresent()
    {
        var otp = OtpSmsMessageBuilder.BuildForSend(
            "9999",
            autofillDomain: "ok-sms.ir",
            androidAppHash: "XyZ123AbCde");

        var prepared = SmsPartsCalculator.PrepareForSend(otp, SmsPartsRules.Defaults);
        Assert.Equal(otp, prepared);
        Assert.DoesNotContain("لغو11\nلغو11", prepared);
    }

    [Fact]
    public void BuildForSend_StaysSinglePersianPart_WithDomain()
    {
        var message = OtpSmsMessageBuilder.BuildForSend(
            "1234",
            autofillDomain: "ok-sms.ir");

        var parts = SmsPartsCalculator.CalculateParts(message, SmsPartsRules.Defaults);
        Assert.Equal(1, parts);
    }
}
