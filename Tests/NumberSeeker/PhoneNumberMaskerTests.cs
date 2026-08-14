using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.NumberSeeker;

public class PhoneNumberMaskerTests
{
    [Theory]
    [InlineData("09121234567", "0912****567")]
    [InlineData("09121111111", "0912****111")]
    [InlineData("02112345678", "0211****678")]
    public void Mask_KeepsPrefixAndSuffixAndSameLength(string input, string expected)
    {
        var masked = PhoneNumberMasker.Mask(input);
        Assert.Equal(expected, masked);
        Assert.Equal(input.Length, masked.Length);
    }

    [Fact]
    public void ForClient_HidesUnlessPrivileged()
    {
        Assert.Equal("0912****567", PhoneNumberMasker.ForClient("09121234567", hideMobileNumber: true, canViewPhones: false));
        Assert.Equal("09121234567", PhoneNumberMasker.ForClient("09121234567", hideMobileNumber: true, canViewPhones: true));
        Assert.Equal("09121234567", PhoneNumberMasker.ForClient("09121234567", hideMobileNumber: false, canViewPhones: false));
    }

    [Fact]
    public void IsMaskedVersionOf_DetectsDisplayValue()
    {
        Assert.True(PhoneNumberMasker.IsMaskedVersionOf("0912****567", "09121234567"));
        Assert.False(PhoneNumberMasker.IsMaskedVersionOf("09121234567", "09121234567"));
    }
}
