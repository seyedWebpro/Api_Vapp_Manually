using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// اعتبارسنجی مقادیر ارسال‌شده در فرم عمومی
    /// </summary>
    public static class UserFormFieldValueValidator
    {
        public static List<string> Validate(
            IReadOnlyList<UserFormField> fields,
            IReadOnlyDictionary<string, string?> values)
        {
            var errors = new List<string>();

            foreach (var field in fields.Where(f => f.IsActive))
            {
                values.TryGetValue(field.FieldKey, out var rawValue);
                var value = rawValue?.Trim();

                if (field.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"فیلد {field.Label} الزامی است");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (IsMobileField(field) && !BookingMobileHelper.IsValidIranianMobile(value))
                {
                    errors.Add($"مقدار فیلد {field.Label} نامعتبر است");
                }
            }

            return errors;
        }

        private static bool IsMobileField(UserFormField field) =>
            string.Equals(field.FieldType, "mobile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.FieldKey, "mobile", StringComparison.OrdinalIgnoreCase);
    }
}
