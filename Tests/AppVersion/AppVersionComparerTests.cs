using Api_Vapp.Constants;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.AppVersion;

public class AppVersionComparerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1", "1.0.0", 0)]
    [InlineData("1.0.0+1", "1.0.0", 0)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.10.0", "1.2.0", 1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    public void Compare_Works(string left, string right, int expectedSign)
    {
        var result = AppVersionComparer.Compare(left, right);
        Assert.Equal(Math.Sign(expectedSign), Math.Sign(result));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", "1.0.0", AppUpdateTypes.None)]
    [InlineData("1.0.0", "1.0.0", "1.1.0", AppUpdateTypes.Optional)]
    [InlineData("1.0.0", "1.1.0", "1.2.0", AppUpdateTypes.Forced)]
    [InlineData("1.1.0", "1.0.0", "1.1.0", AppUpdateTypes.None)]
    public void ResolveUpdateType_Works(
        string current,
        string min,
        string latest,
        string expected)
    {
        var result = AppVersionComparer.ResolveUpdateType(current, min, latest);
        Assert.Equal(expected, result);
    }
}
