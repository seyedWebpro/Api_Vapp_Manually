namespace Api_Vapp.Utilities
{
    /// <summary>
    /// مقایسه semantic version برای چک آپدیت اپ (مثلاً 1.0.0 ، 1.2 ، 2).
    /// </summary>
    public static class AppVersionComparer
    {
        public static int Compare(string? left, string? right)
        {
            var leftVersion = Parse(left);
            var rightVersion = Parse(right);
            return leftVersion.CompareTo(rightVersion);
        }

        public static bool TryParse(string? input, out Version version)
        {
            version = new Version(0, 0, 0);
            var normalized = Normalize(input);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            return Version.TryParse(normalized, out version!);
        }

        public static Version Parse(string? input)
        {
            if (TryParse(input, out var version))
                return version;

            return new Version(0, 0, 0);
        }

        /// <summary>
        /// current &lt; min → forced؛ current &lt; latest → optional؛ در غیر این صورت none.
        /// </summary>
        public static string ResolveUpdateType(string currentVersion, string minSupported, string latest)
        {
            if (Compare(currentVersion, minSupported) < 0)
                return Constants.AppUpdateTypes.Forced;

            if (Compare(currentVersion, latest) < 0)
                return Constants.AppUpdateTypes.Optional;

            return Constants.AppUpdateTypes.None;
        }

        /// <summary>
        /// build number بعد از + را حذف می‌کند و بخش‌های ناقص را تا سه قسمت پر می‌کند.
        /// </summary>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var value = input.Trim();
            var plusIndex = value.IndexOf('+');
            if (plusIndex >= 0)
                value = value[..plusIndex];

            var dashIndex = value.IndexOf('-');
            if (dashIndex >= 0)
                value = value[..dashIndex];

            value = value.Trim();
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return string.Empty;

            while (parts.Length < 3)
                parts = [.. parts, "0"];

            return string.Join('.', parts.Take(4));
        }
    }
}
