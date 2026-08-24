using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.QuickSend
{
    public class QuickSendLinkSmsHelperTests
    {
        [Fact]
        public void BuildSmsContent_WithoutCaption_ReturnsUrlOnly()
        {
            var result = QuickSendLinkSmsHelper.BuildSmsContent(null, "https://app.com/card/abc");
            Assert.Equal("https://app.com/card/abc", result);
        }

        [Fact]
        public void BuildSmsContent_WithWhitespaceCaption_ReturnsUrlOnly()
        {
            var result = QuickSendLinkSmsHelper.BuildSmsContent("   ", "https://app.com/book/x");
            Assert.Equal("https://app.com/book/x", result);
        }

        [Fact]
        public void BuildSmsContent_WithCaption_JoinsCaptionAndUrl()
        {
            var result = QuickSendLinkSmsHelper.BuildSmsContent(
                "  رزرو آنلاین کلینیک  ",
                "https://app.com/book/clinic");

            Assert.Equal("رزرو آنلاین کلینیک\nhttps://app.com/book/clinic", result);
        }

        [Fact]
        public void NormalizeCaption_EmptyBecomesNull()
        {
            Assert.Null(QuickSendLinkSmsHelper.NormalizeCaption(""));
            Assert.Null(QuickSendLinkSmsHelper.NormalizeCaption("   "));
            Assert.Equal("عنوان", QuickSendLinkSmsHelper.NormalizeCaption("  عنوان  "));
        }

        [Fact]
        public void NormalizeCaption_CollapsesInternalWhitespaceAndNewlines()
        {
            Assert.Equal("عنوان تست", QuickSendLinkSmsHelper.NormalizeCaption("عنوان\n  تست"));
            Assert.Equal("a b", QuickSendLinkSmsHelper.NormalizeCaption("a\r\nb"));
        }

        [Fact]
        public void NormalizeCaption_TruncatesOverMaxLength()
        {
            var input = new string('ا', QuickSendLinkSmsHelper.MaxCaptionLength + 20);
            var result = QuickSendLinkSmsHelper.NormalizeCaption(input);
            Assert.NotNull(result);
            Assert.Equal(QuickSendLinkSmsHelper.MaxCaptionLength, result!.Length);
        }

        [Fact]
        public void BuildSmsContent_NormalizesCaptionBeforeJoin()
        {
            var result = QuickSendLinkSmsHelper.BuildSmsContent(
                "خط۱\nخط۲",
                "https://app.com/f/x");

            Assert.Equal("خط۱ خط۲\nhttps://app.com/f/x", result);
        }
    }
}
