using System.Text;
using System.Text.RegularExpressions;

namespace Api_Vapp.Utilities
{
    public static class UserFormSlugHelper
    {
        private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public static string? Normalize(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var normalized = slug.Trim().ToLowerInvariant().Replace('_', '-');

            if (!SlugPattern.IsMatch(normalized))
            {
                return null;
            }

            return normalized.Length > UserFormConstants.MaxSlugLength
                ? normalized[..UserFormConstants.MaxSlugLength].Trim('-')
                : normalized;
        }

        public static string SlugifyTitle(string title)
        {
            var normalized = title.Trim().ToLowerInvariant();
            var builder = new StringBuilder();

            foreach (var ch in normalized)
            {
                if (char.IsAsciiLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
                else if (ch is ' ' or '-' or '_')
                {
                    if (builder.Length > 0 && builder[^1] != '-')
                    {
                        builder.Append('-');
                    }
                }
            }

            var slug = builder.ToString().Trim('-');
            if (string.IsNullOrWhiteSpace(slug))
            {
                return "form";
            }

            return slug.Length > 80 ? slug[..80].Trim('-') : slug;
        }

        public static string BuildCandidateSlug(string baseSlug, int suffix)
        {
            return suffix == 0 ? baseSlug : $"{baseSlug}-{suffix}";
        }
    }
}
