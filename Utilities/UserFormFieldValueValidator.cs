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
                else if (IsEmailField(field) && !IsValidEmail(value))
                {
                    errors.Add($"مقدار فیلد {field.Label} نامعتبر است");
                }
                else if (IsNumberField(field) && !decimal.TryParse(value, out _))
                {
                    errors.Add($"مقدار فیلد {field.Label} باید عددی باشد");
                }
            }

            return errors;
        }

        private static bool IsMobileField(UserFormField field) =>
            string.Equals(field.FieldType, "mobile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.FieldKey, "mobile", StringComparison.OrdinalIgnoreCase);

        private static bool IsEmailField(UserFormField field) =>
            string.Equals(field.FieldType, "email", StringComparison.OrdinalIgnoreCase);

        private static bool IsNumberField(UserFormField field) =>
            string.Equals(field.FieldType, "number", StringComparison.OrdinalIgnoreCase);

        private static bool IsValidEmail(string value)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(value);
                return string.Equals(addr.Address, value, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
