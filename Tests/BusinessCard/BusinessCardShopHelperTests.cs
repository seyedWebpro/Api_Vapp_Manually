using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.BusinessCard;

public class BusinessCardShopHelperTests
{
    [Theory]
    [InlineData("myshop.ir", "https://myshop.ir/")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    [InlineData("http://shop.test", "http://shop.test/")]
    [InlineData("localhost:3000", "https://localhost:3000/")]
    public void NormalizeUrl_ValidInput_ReturnsNormalizedUrl(string input, string expected)
    {
        var (normalized, error) = BusinessCardShopHelper.NormalizeUrl(input);

        Assert.Null(error);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeUrl_EmptyInput_ReturnsNull(string? input)
    {
        var (normalized, error) = BusinessCardShopHelper.NormalizeUrl(input);

        Assert.Null(error);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a valid url!!!")]
    [InlineData("shop")]
    [InlineData("user@shop.com")]
    public void NormalizeUrl_InvalidInput_ReturnsPersianError(string input)
    {
        var (normalized, error) = BusinessCardShopHelper.NormalizeUrl(input);

        Assert.Null(normalized);
        Assert.NotNull(error);
        Assert.DoesNotContain("Exception", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constants_MatchProductCopy()
    {
        Assert.Equal("فروشگاه", BusinessCardShopHelper.ButtonLabel);
        Assert.Equal("02151091000", BusinessCardShopHelper.ContactPhone);
        Assert.Contains("۰۲۱۵۱۰۹۱۰۰۰", BusinessCardShopHelper.NoStoreHint);
    }
}
