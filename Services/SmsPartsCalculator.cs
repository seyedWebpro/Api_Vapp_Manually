using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Api_Vapp.Services
{
    /// <summary>
    /// کلاس کمکی برای محاسبه دقیق تعداد پارت‌های پیامک
    /// بر اساس نوع زبان (فارسی/انگلیسی) و قوانین اپراتور
    /// </summary>
    public static class SmsPartsCalculator
    {
        // محدودیت‌های پیامک فارسی
        private const int PERSIAN_FIRST_PAGE_CHARS = 70;
        private const int PERSIAN_SECOND_PAGE_CHARS = 64;
        private const int PERSIAN_OTHER_PAGES_CHARS = 67;

        // محدودیت‌های پیامک انگلیسی
        private const int ENGLISH_FIRST_PAGE_CHARS = 160;
        private const int ENGLISH_OTHER_PAGES_CHARS = 153;

        // حداکثر تعداد صفحات مجاز
        private const int MAX_PAGES = 10;

        /// <summary>
        /// محاسبه تعداد پارت‌های پیامک بر اساس محتوا
        /// </summary>
        /// <param name="content">محتوی پیامک</param>
        /// <returns>تعداد پارت‌های پیامک</returns>
        /// <exception cref="ArgumentException">اگر تعداد صفحات بیشتر از 10 باشد</exception>
        public static int CalculateParts(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 1;

            var trimmedContent = content.Trim();
            if (string.IsNullOrEmpty(trimmedContent))
                return 1;

            // تشخیص زبان بر اساس ابتدای پیام
            bool isPersian = DetectLanguage(trimmedContent);

            // شمارش دقیق کاراکترها (با در نظر گیری ایموجی و فاصله)
            int totalChars = CountCharactersInternal(trimmedContent);

            // محاسبه تعداد صفحات
            int pages = CalculatePages(totalChars, isPersian);

            // اعتبارسنجی حداکثر صفحات
            if (pages > MAX_PAGES)
            {
                throw new ArgumentException(
                    $"تعداد صفحات پیامک ({pages}) از حداکثر مجاز ({MAX_PAGES} صفحه) بیشتر است. لطفاً محتوا را کوتاه کنید.",
                    nameof(content));
            }

            return pages;
        }

        /// <summary>
        /// محاسبه تعداد صفحات پیامک بر اساس تعداد کاراکترها و نوع زبان
        /// </summary>
        private static int CalculatePages(int totalChars, bool isPersian)
        {
            if (isPersian)
            {
                return CalculatePersianPages(totalChars);
            }
            else
            {
                return CalculateEnglishPages(totalChars);
            }
        }

        /// <summary>
        /// محاسبه تعداد صفحات پیامک فارسی
        /// صفحه اول: 70 کاراکتر
        /// صفحه دوم: 64 کاراکتر
        /// صفحات بعدی: 67 کاراکتر
        /// </summary>
        private static int CalculatePersianPages(int totalChars)
        {
            if (totalChars <= PERSIAN_FIRST_PAGE_CHARS)
                return 1;

            // کم کردن کاراکترهای صفحه اول
            int remainingChars = totalChars - PERSIAN_FIRST_PAGE_CHARS;
            int pages = 1;

            if (remainingChars <= PERSIAN_SECOND_PAGE_CHARS)
                return 2;

            // کم کردن کاراکترهای صفحه دوم
            remainingChars -= PERSIAN_SECOND_PAGE_CHARS;
            pages = 2;

            // محاسبه صفحات باقی‌مانده
            pages += (int)Math.Ceiling((double)remainingChars / PERSIAN_OTHER_PAGES_CHARS);

            return pages;
        }

        /// <summary>
        /// محاسبه تعداد صفحات پیامک انگلیسی
        /// صفحه اول: 160 کاراکتر
        /// صفحات بعدی: 153 کاراکتر
        /// </summary>
        private static int CalculateEnglishPages(int totalChars)
        {
            if (totalChars <= ENGLISH_FIRST_PAGE_CHARS)
                return 1;

            // کم کردن کاراکترهای صفحه اول
            int remainingChars = totalChars - ENGLISH_FIRST_PAGE_CHARS;

            // محاسبه صفحات باقی‌مانده
            int additionalPages = (int)Math.Ceiling((double)remainingChars / ENGLISH_OTHER_PAGES_CHARS);

            return 1 + additionalPages;
        }

        /// <summary>
        /// تشخیص زبان پیام بر اساس ابتدای محتوا
        /// اگر فارسی باشد true و اگر انگلیسی باشد false برمی‌گرداند
        /// </summary>
        private static bool DetectLanguage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return true; // پیش‌فرض فارسی

            // بررسی ابتدای پیام (حدود 50 کاراکتر اول)
            string sample = content.Length > 50 ? content.Substring(0, 50) : content;

            // بررسی وجود کاراکترهای فارسی
            // محدوده یونیکد کاراکترهای فارسی: U+0600 تا U+06FF
            bool hasPersianChars = false;
            bool hasEnglishChars = false;

            foreach (char c in sample)
            {
                // بررسی کاراکترهای فارسی (شامل اعداد فارسی و علائم نگارشی فارسی)
                if ((c >= 0x0600 && c <= 0x06FF) || 
                    (c >= 0xFB50 && c <= 0xFDFF) || 
                    (c >= 0xFE70 && c <= 0xFEFF))
                {
                    hasPersianChars = true;
                }
                // بررسی کاراکترهای انگلیسی (حروف و اعداد انگلیسی)
                else if ((c >= 0x0020 && c <= 0x007E) || // ASCII printable
                         (c >= 0x00A0 && c <= 0x00FF))   // Extended ASCII
                {
                    // نادیده گرفتن فاصله‌ها و علائم نگارشی برای تصمیم‌گیری بهتر
                    if (char.IsLetter(c) || char.IsDigit(c))
                    {
                        hasEnglishChars = true;
                    }
                }
            }

            // اگر کاراکتر فارسی پیدا شد، پیام فارسی است
            if (hasPersianChars)
                return true;

            // اگر فقط کاراکتر انگلیسی پیدا شد، پیام انگلیسی است
            if (hasEnglishChars)
                return false;

            // پیش‌فرض: فارسی
            return true;
        }

        /// <summary>
        /// شمارش دقیق کاراکترها با در نظر گیری:
        /// - ایموجی: 3 کاراکتر
        /// - فاصله: 1 کاراکتر
        /// - سایر کاراکترها: 1 کاراکتر
        /// </summary>
        private static int CountCharactersInternal(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 0;

            int count = 0;
            var textElements = System.Globalization.StringInfo.GetTextElementEnumerator(content);

            while (textElements.MoveNext())
            {
                string element = textElements.GetTextElement();
                
                // بررسی اینکه آیا این یک ایموجی است یا خیر
                if (IsEmoji(element))
                {
                    count += 3; // هر ایموجی 3 کاراکتر محاسبه می‌شود
                }
                else
                {
                    count += 1; // سایر کاراکترها (شامل فاصله) 1 کاراکتر محاسبه می‌شوند
                }
            }

            return count;
        }

        /// <summary>
        /// بررسی اینکه آیا یک رشته یک ایموجی است یا خیر
        /// پشتیبانی از ایموجی‌های تک کاراکتری و surrogate pairs (4 بایتی)
        /// </summary>
        private static bool IsEmoji(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // پردازش متن به صورت Text Elements برای مدیریت صحیح surrogate pairs
            var textElements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            
            while (textElements.MoveNext())
            {
                string element = textElements.GetTextElement();
                
                // برای surrogate pairs (ایموجی‌های 4 بایتی)
                if (element.Length >= 2)
                {
                    int highSurrogate = char.ConvertToUtf32(element, 0);
                    
                    // بررسی محدوده‌های یونیکد ایموجی‌های 4 بایتی
                    if ((highSurrogate >= 0x1F300 && highSurrogate <= 0x1F9FF) || // Emoticons, Symbols & Pictographs
                        (highSurrogate >= 0x1F900 && highSurrogate <= 0x1F9FF) || // Supplemental Symbols and Pictographs
                        (highSurrogate >= 0x1F1E0 && highSurrogate <= 0x1F1FF))   // Regional Indicator Symbols (flags)
                    {
                        return true;
                    }
                }
                else if (element.Length == 1)
                {
                    int codePoint = char.ConvertToUtf32(element, 0);
                    
                    // بررسی محدوده‌های یونیکد ایموجی‌های تک کاراکتری
                    if ((codePoint >= 0x2600 && codePoint <= 0x26FF) ||   // Miscellaneous Symbols
                        (codePoint >= 0x2700 && codePoint <= 0x27BF) ||   // Dingbats
                        (codePoint >= 0xFE00 && codePoint <= 0xFE0F) ||   // Variation Selectors
                        (codePoint == 0x200D) ||                          // Zero Width Joiner (برای ترکیب ایموجی)
                        (codePoint == 0x20E3))                            // Combining Enclosing Keycap
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// محاسبه تعداد کاراکترهای پیام (با در نظر گیری ایموجی و فاصله)
        /// برای استفاده در نمایش اطلاعات به کاربر
        /// </summary>
        public static int CountMessageCharacters(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 0;

            return CountCharactersInternal(content);
        }

        /// <summary>
        /// تشخیص نوع زبان پیام
        /// </summary>
        /// <param name="content">محتوی پیام</param>
        /// <returns>true اگر فارسی باشد، false اگر انگلیسی باشد</returns>
        public static bool IsPersian(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return true;

            return DetectLanguage(content);
        }
    }
}

