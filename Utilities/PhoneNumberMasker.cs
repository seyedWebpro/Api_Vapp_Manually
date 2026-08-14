namespace Api_Vapp.Utilities
{
    /// <summary>
    /// ماسک شماره موبایل برای نمایش به کاربر عادی — طول رشته حفظ می‌شود تا اکسل و UI خراب نشود.
    /// مثال: 09121234567 → 0912****567
    /// </summary>
    public static class PhoneNumberMasker
    {
        public const int VisiblePrefixLength = 4;
        public const int VisibleSuffixLength = 3;

        public static string Mask(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone ?? string.Empty;

            var value = phone.Trim();
            if (value.Length <= VisiblePrefixLength + VisibleSuffixLength)
            {
                if (value.Length <= 2)
                    return new string('*', value.Length);

                const int keep = 1;
                return value[..keep] + new string('*', value.Length - (keep * 2)) + value[^keep..];
            }

            var starCount = value.Length - VisiblePrefixLength - VisibleSuffixLength;
            return value[..VisiblePrefixLength] + new string('*', starCount) + value[^VisibleSuffixLength..];
        }

        public static string ForClient(string? phone, bool hideMobileNumber, bool canViewPhones)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone ?? string.Empty;

            if (!hideMobileNumber || canViewPhones)
                return phone;

            return Mask(phone);
        }

        public static List<string> ForClient(IEnumerable<string>? phones, bool canViewPhones)
        {
            var list = phones?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? new List<string>();

            if (canViewPhones)
                return list;

            return list.Select(Mask).ToList();
        }

        public static bool IsMaskedVersionOf(string? maybeMasked, string? real)
        {
            if (string.IsNullOrWhiteSpace(maybeMasked) || string.IsNullOrWhiteSpace(real))
                return false;

            return string.Equals(Mask(real), maybeMasked.Trim(), StringComparison.Ordinal);
        }
    }
}
