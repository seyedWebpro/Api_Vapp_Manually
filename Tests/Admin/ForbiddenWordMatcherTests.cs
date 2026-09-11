using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class ForbiddenWordMatcherTests
{
    [Fact]
    public void Normalize_MapsArabicVariants()
    {
        Assert.Equal("کیک", ForbiddenWordMatcher.Normalize("كيك"));
        Assert.Equal("یاری", ForbiddenWordMatcher.Normalize("ياري"));
    }

    [Fact]
    public void FindMatches_DetectsWholeWord()
    {
        var forbidden = new List<(string Display, string Normalized)>
        {
            ("قمار", ForbiddenWordMatcher.Normalize("قمار"))
        };

        var matches = ForbiddenWordMatcher.FindMatches("این متن قمار دارد", forbidden);
        Assert.Single(matches);
        Assert.Equal("قمار", matches[0]);
    }

    [Fact]
    public void FindMatches_IgnoresPartialWord()
    {
        var forbidden = new List<(string Display, string Normalized)>
        {
            ("کار", ForbiddenWordMatcher.Normalize("کار"))
        };

        // «کار» داخل «همکاری» نباید به‌تنهایی تطبیق شود
        var matches = ForbiddenWordMatcher.FindMatches("همکاری خوبی داشتیم", forbidden);
        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_SupportsMultiWordPhrase()
    {
        var forbidden = new List<(string Display, string Normalized)>
        {
            ("پول نقد", ForbiddenWordMatcher.Normalize("پول نقد"))
        };

        var matches = ForbiddenWordMatcher.FindMatches("پرداخت با پول نقد انجام شود", forbidden);
        Assert.Single(matches);
    }
}
