using System.Text.RegularExpressions;

namespace Api_Vapp.Utilities;

/// <summary>
/// تبدیل لینک ذخیره‌شده ویدیو آموزشی به آدرس قابل‌پخش داخل اپ (مثلاً embed آپارات).
/// </summary>
public static class EducationalVideoPlaybackHelper
{
    public const string ModeAparatEmbed = "aparat_embed";
    public const string ModeFile = "file";
    public const string ModeDirect = "direct";

    private static readonly Regex AparatHashFromPath = new(
        @"^/v/([A-Za-z0-9_-]+)/?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AparatHashFromEmbed = new(
        @"/embed/videohash/([A-Za-z0-9_-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// لینک مناسب پخش داخل اپ/WebView (برای آپارات: embed؛ برای فایل/لینک مستقیم: همان videoUrl).
    /// </summary>
    public static string ResolvePlaybackUrl(string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return string.Empty;

        var trimmed = videoUrl.Trim();
        if (TryGetAparatEmbedUrl(trimmed, out var embed))
            return embed;

        return trimmed;
    }

    public static string ResolvePlaybackMode(string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return ModeDirect;

        var trimmed = videoUrl.Trim();
        if (IsUploadedVideoPath(trimmed))
            return ModeFile;

        if (TryGetAparatEmbedUrl(trimmed, out _))
            return ModeAparatEmbed;

        return ModeDirect;
    }

    public static bool TryGetAparatEmbedUrl(string videoUrl, out string embedUrl)
    {
        embedUrl = string.Empty;
        if (!Uri.TryCreate(videoUrl.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!IsAparatHost(uri.Host))
            return false;

        var embedMatch = AparatHashFromEmbed.Match(uri.AbsolutePath);
        if (embedMatch.Success)
        {
            embedUrl = BuildAparatEmbedUrl(embedMatch.Groups[1].Value);
            return true;
        }

        var pathMatch = AparatHashFromPath.Match(uri.AbsolutePath);
        if (pathMatch.Success)
        {
            embedUrl = BuildAparatEmbedUrl(pathMatch.Groups[1].Value);
            return true;
        }

        return false;
    }

    public static string BuildAparatEmbedUrl(string hash) =>
        $"https://www.aparat.com/video/video/embed/videohash/{hash}/vt/frame";

    private static bool IsAparatHost(string host) =>
        host.Equals("aparat.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".aparat.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsUploadedVideoPath(string path)
    {
        var normalized = path.Replace("\\", "/").TrimStart('/');
        return normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);
    }
}
