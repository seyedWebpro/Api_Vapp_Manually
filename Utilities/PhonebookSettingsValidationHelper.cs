using Api_Vapp.DTOs.UserForm;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// اعتبارسنجی و پیام‌های کنترل‌شده برای «ذخیره در دفترچه تلفن».
    /// پیام اصلی پاسخ API همان متن قابل‌نمایش به کاربر است (اپ معمولاً فقط message را نشان می‌دهد).
    /// </summary>
    public static class PhonebookSettingsValidationHelper
    {
        public const string MissingNotebookMessage =
            "ذخیره در دفترچه فعال است؛ لطفاً حداقل یک دفترچه تلفن انتخاب کنید";

        public const string MissingMobileFieldMessage =
            "ذخیره در دفترچه فعال است؛ یک فیلد موبایل فعال در فرم لازم است";

        public static List<string> ValidateNotebookSelection(
            bool saveToPhonebook,
            IReadOnlyList<int> notebookIds)
        {
            var errors = new List<string>();

            if (!saveToPhonebook)
            {
                return errors;
            }

            if (notebookIds.Count == 0)
            {
                errors.Add(MissingNotebookMessage);
            }

            return errors;
        }

        public static List<string> ValidateForUserForm(
            bool saveToPhonebook,
            IReadOnlyList<int> notebookIds,
            IReadOnlyList<UserFormFieldDto> fields)
        {
            var errors = ValidateNotebookSelection(saveToPhonebook, notebookIds);

            if (!saveToPhonebook)
            {
                return errors;
            }

            var hasMobileField = fields.Any(f =>
                f.IsActive &&
                (string.Equals(f.FieldKey, "mobile", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(f.FieldType, "mobile", StringComparison.OrdinalIgnoreCase)));

            if (!hasMobileField)
            {
                errors.Add(MissingMobileFieldMessage);
            }

            return errors;
        }

        /// <summary>
        /// پیام اصلی برای ApiResponse.Message — اپ موبایل معمولاً فقط همین را نشان می‌دهد.
        /// </summary>
        public static string ToUserMessage(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return "تنظیمات ذخیره در دفترچه تلفن ناقص است";
            }

            if (errors.Count == 1)
            {
                return errors[0];
            }

            return string.Join("؛ ", errors);
        }
    }
}
