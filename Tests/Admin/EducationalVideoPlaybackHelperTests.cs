using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class EducationalVideoPlaybackHelperTests
{
    [Theory]
    [InlineData("https://www.aparat.com/v/ujm1mw5", "ujm1mw5")]
    [InlineData("https://aparat.com/v/ujm1mw5/", "ujm1mw5")]
    [InlineData("https://www.aparat.com/video/video/embed/videohash/ujm1mw5/vt/frame", "ujm1mw5")]
    public void Aparat_PageOrEmbed_ResolvesToEmbed(string input, string hash)
    {
        var expected = EducationalVideoPlaybackHelper.BuildAparatEmbedUrl(hash);
        Assert.Equal(expected, EducationalVideoPlaybackHelper.ResolvePlaybackUrl(input));
        Assert.Equal(EducationalVideoPlaybackHelper.ModeAparatEmbed, EducationalVideoPlaybackHelper.ResolvePlaybackMode(input));
    }

    [Fact]
    public void UploadedFile_KeepsPath_AndModeFile()
    {
        const string path = "uploads/educationalvideo/1/videos/a.mp4";
        Assert.Equal(path, EducationalVideoPlaybackHelper.ResolvePlaybackUrl(path));
        Assert.Equal(EducationalVideoPlaybackHelper.ModeFile, EducationalVideoPlaybackHelper.ResolvePlaybackMode(path));
    }

    [Fact]
    public void DirectHttps_KeepsUrl_AndModeDirect()
    {
        const string url = "https://cdn.example.com/lesson.mp4";
        Assert.Equal(url, EducationalVideoPlaybackHelper.ResolvePlaybackUrl(url));
        Assert.Equal(EducationalVideoPlaybackHelper.ModeDirect, EducationalVideoPlaybackHelper.ResolvePlaybackMode(url));
    }
}
