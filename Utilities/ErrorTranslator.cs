namespace Api_Vapp.Utilities
{
    /// <summary>
    /// کلاس کمکی برای ترجمه خطاهای انگلیسی به فارسی
    /// </summary>
    public static class ErrorTranslator
    {
        /// <summary>
        /// استخراج امن خطاهای ModelState بدون نشت ExceptionMessage خام
        /// </summary>
        public static List<string> ExtractModelStateErrors(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
        {
            return modelState
                .Where(e => e.Value is { Errors.Count: > 0 })
                .SelectMany(x => x.Value!.Errors.Select(error =>
                {
                    var errorMessage = error.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(errorMessage))
                        errorMessage = "مقدار وارد شده نامعتبر است";

                    return TranslateValidationError(errorMessage, x.Key);
                }))
                .ToList();
        }

        /// <summary>
        /// تبدیل خطاهای اعتبارسنجی انگلیسی به فارسی
        /// </summary>
        public static string TranslateValidationError(string errorMessage, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return "مقدار وارد شده نامعتبر است";

            // اگر خطا قبلاً فارسی است، همان را برمی‌گردانیم
            if (errorMessage.Contains("الزامی") || errorMessage.Contains("صحیح نیست") || 
                errorMessage.Contains("نمی‌تواند") || errorMessage.Contains("باید") ||
                errorMessage.Contains("نامعتبر") || errorMessage.Contains("باید بین"))
            {
                return errorMessage;
            }

            var errorLower = errorMessage.ToLower();
            
            // خطاهای مربوط به Required
            if (errorLower.Contains("required") || errorLower.Contains("field is required") ||
                errorLower.Contains("is required"))
            {
                var fieldNamePersian = GetFieldNamePersian(fieldName);
                return $"{fieldNamePersian} الزامی است";
            }

            // خطاهای مربوط به Format
            if (errorLower.Contains("format") || errorLower.Contains("invalid format") ||
                (errorLower.Contains("invalid") && !errorLower.Contains("length")))
            {
                if (fieldName.Contains("Mobile") || fieldName.Contains("Phone"))
                    return "فرمت شماره موبایل صحیح نیست (باید با 09 شروع شود و 11 رقم باشد)";
                if (fieldName.Contains("Email"))
                    return "فرمت ایمیل صحیح نیست";
                if (fieldName.Contains("NationalId"))
                    return "فرمت کد ملی صحیح نیست";
                if (fieldName.Contains("Date") || fieldName.Contains("date"))
                    return "فرمت تاریخ صحیح نیست";
                return "فرمت داده وارد شده صحیح نیست";
            }

            // خطاهای مربوط به Length
            if (errorLower.Contains("length") || errorLower.Contains("maximum") ||
                errorLower.Contains("minimum") || errorLower.Contains("too long") ||
                errorLower.Contains("too short"))
            {
                return TranslateLengthError(errorMessage, errorLower);
            }

            // خطاهای مربوط به Range
            if (errorLower.Contains("range") || errorLower.Contains("between") ||
                errorLower.Contains("out of range"))
            {
                return TranslateRangeError(errorMessage);
            }

            // خطاهای مربوط به Compare
            if (errorLower.Contains("compare") || errorLower.Contains("match") ||
                errorLower.Contains("do not match"))
            {
                if (fieldName.Contains("Password") || fieldName.Contains("Confirm"))
                    return "رمز عبور و تکرار آن باید یکسان باشند";
                return "مقادیر وارد شده یکسان نیستند";
            }

            // برای سایر خطاها، پیام اصلی را برمی‌گردانیم
            return errorMessage;
        }

        /// <summary>
        /// تبدیل نام فیلد انگلیسی به فارسی
        /// </summary>
        private static string GetFieldNamePersian(string fieldName)
        {
            return fieldName switch
            {
                "FullName" or "fullName" => "نام خانوادگی",
                "MobileNumber" or "mobileNumber" => "شماره موبایل",
                "ContactNotebookId" or "contactNotebookId" => "شناسه دفترچه",
                "PhoneNumber" or "phoneNumber" => "شماره تلفن",
                "Password" or "password" => "رمز عبور",
                "NationalId" or "nationalId" => "کد ملی",
                "Email" or "email" => "ایمیل",
                "Title" or "title" => "عنوان",
                "Content" or "content" => "محتوا",
                "Amount" or "amount" => "مبلغ",
                "Percent" or "percent" => "درصد",
                "Duration" or "duration" => "مدت",
                _ => fieldName
            };
        }

        /// <summary>
        /// ترجمه خطاهای مربوط به طول
        /// </summary>
        private static string TranslateLengthError(string errorMessage, string errorLower)
        {
            // استخراج عدد از پیام خطا
            var numbers = System.Text.RegularExpressions.Regex.Matches(errorMessage, @"\d+");
            if (numbers.Count > 0)
            {
                var length = numbers[numbers.Count - 1].Value;
                if (errorLower.Contains("maximum") || errorLower.Contains("too long"))
                    return $"مقدار نمی‌تواند بیشتر از {length} کاراکتر باشد";
                if (errorLower.Contains("minimum") || errorLower.Contains("too short"))
                    return $"مقدار نمی‌تواند کمتر از {length} کاراکتر باشد";
            }
            
            // بررسی مقادیر رایج
            if (errorLower.Contains("200"))
                return "مقدار نمی‌تواند بیشتر از 200 کاراکتر باشد";
            if (errorLower.Contains("100"))
                return "مقدار نمی‌تواند بیشتر از 100 کاراکتر باشد";
            if (errorLower.Contains("50"))
                return "مقدار نمی‌تواند بیشتر از 50 کاراکتر باشد";
            if (errorLower.Contains("20"))
                return "مقدار نمی‌تواند بیشتر از 20 کاراکتر باشد";
            
            return "طول مقدار وارد شده نامعتبر است";
        }

        /// <summary>
        /// ترجمه خطاهای مربوط به محدوده
        /// </summary>
        private static string TranslateRangeError(string errorMessage)
        {
            var numbers = System.Text.RegularExpressions.Regex.Matches(errorMessage, @"\d+");
            if (numbers.Count >= 2)
            {
                var min = numbers[0].Value;
                var max = numbers[numbers.Count - 1].Value;
                return $"مقدار باید بین {min} تا {max} باشد";
            }
            return "مقدار وارد شده خارج از محدوده مجاز است";
        }
    }
}

