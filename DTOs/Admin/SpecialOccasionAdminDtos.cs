using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    /// <summary>نمایش مناسبت سیستمی/پیش‌فرض در پنل ادمین</summary>
    public class SpecialOccasionAdminResponseDto
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryPersian { get; set; } = string.Empty;
        public string CalendarType { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Day { get; set; }
        public DateTime OccasionDate { get; set; }
        public string? DefaultMessage { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsFromCatalog { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>ایجاد مناسبت سیستمی توسط ادمین</summary>
    public class CreateSpecialOccasionAdminDto
    {
        [Required(ErrorMessage = "نام مناسبت الزامی است")]
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        /// <summary>کد یکتا اختیاری — اگر خالی باشد خودکار ساخته می‌شود</summary>
        [MaxLength(80, ErrorMessage = "کد مناسبت نمی‌تواند بیشتر از ۸۰ کاراکتر باشد")]
        [RegularExpression(@"^$|^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "کد مناسبت باید با حرف انگلیسی شروع شود و فقط شامل حرف، عدد و _ باشد")]
        public string? Code { get; set; }

        /// <summary>Holiday | Death | Custom</summary>
        [MaxLength(50, ErrorMessage = "نوع مناسبت نامعتبر است")]
        public string Type { get; set; } = "Custom";

        /// <summary>Congratulation | Condolence — اگر خالی باشد از Type استنباط می‌شود</summary>
        [MaxLength(30, ErrorMessage = "دسته‌بندی مناسبت نامعتبر است")]
        public string? Category { get; set; }

        /// <summary>Jalali | Gregorian | Hijri</summary>
        [MaxLength(20, ErrorMessage = "نوع تقویم نامعتبر است")]
        public string CalendarType { get; set; } = "Jalali";

        [Required(ErrorMessage = "ماه الزامی است")]
        [Range(1, 12, ErrorMessage = "ماه باید بین ۱ تا ۱۲ باشد")]
        public int Month { get; set; }

        [Required(ErrorMessage = "روز الزامی است")]
        [Range(1, 31, ErrorMessage = "روز باید بین ۱ تا ۳۱ باشد")]
        public int Day { get; set; }

        [MaxLength(2000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد")]
        public string? DefaultMessage { get; set; }

        public int SortOrder { get; set; } = 1000;

        public bool IsActive { get; set; } = true;
    }

    /// <summary>به‌روزرسانی مناسبت سیستمی توسط ادمین</summary>
    public class UpdateSpecialOccasionAdminDto
    {
        [Required(ErrorMessage = "نام مناسبت الزامی است")]
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "نوع مناسبت نامعتبر است")]
        public string Type { get; set; } = "Custom";

        [MaxLength(30, ErrorMessage = "دسته‌بندی مناسبت نامعتبر است")]
        public string? Category { get; set; }

        [MaxLength(20, ErrorMessage = "نوع تقویم نامعتبر است")]
        public string CalendarType { get; set; } = "Jalali";

        [Required(ErrorMessage = "ماه الزامی است")]
        [Range(1, 12, ErrorMessage = "ماه باید بین ۱ تا ۱۲ باشد")]
        public int Month { get; set; }

        [Required(ErrorMessage = "روز الزامی است")]
        [Range(1, 31, ErrorMessage = "روز باید بین ۱ تا ۳۱ باشد")]
        public int Day { get; set; }

        [MaxLength(2000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد")]
        public string? DefaultMessage { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
