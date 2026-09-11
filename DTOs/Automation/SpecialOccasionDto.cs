using System.ComponentModel.DataAnnotations;
using Api_Vapp.Constants;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ایجاد مناسبت خاص (سفارشی کاربر)
    /// </summary>
    public class CreateSpecialOccasionDto
    {
        [Required(ErrorMessage = "نام مناسبت الزامی است")]
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Holiday | Death | Custom — پیش‌فرض Custom</summary>
        [MaxLength(50, ErrorMessage = "نوع مناسبت نامعتبر است")]
        public string Type { get; set; } = OccasionTypeCodes.Custom;

        /// <summary>Congratulation | Condolence — اگر خالی باشد از Type استنباط می‌شود</summary>
        [MaxLength(30, ErrorMessage = "دسته‌بندی مناسبت نامعتبر است")]
        public string? Category { get; set; }

        /// <summary>Jalali | Gregorian | Hijri — پیش‌فرض Jalali</summary>
        [MaxLength(20, ErrorMessage = "نوع تقویم نامعتبر است")]
        public string CalendarType { get; set; } = OccasionCalendarTypes.Jalali;

        /// <summary>ماه در تقویم انتخابی (۱–۱۲). اگر خالی باشد از OccasionDate استخراج می‌شود.</summary>
        [Range(1, 12, ErrorMessage = "ماه باید بین ۱ تا ۱۲ باشد")]
        public int? Month { get; set; }

        /// <summary>روز در تقویم انتخابی. اگر خالی باشد از OccasionDate استخراج می‌شود.</summary>
        [Range(1, 31, ErrorMessage = "روز باید بین ۱ تا ۳۱ باشد")]
        public int? Day { get; set; }

        /// <summary>تاریخ مرجع (سازگاری). برای جلالی بهتر است Month/Day ارسال شود.</summary>
        public DateTime? OccasionDate { get; set; }

        [MaxLength(2000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد")]
        public string? DefaultMessage { get; set; }
    }

    /// <summary>
    /// DTO برای به‌روزرسانی مناسبت خاص
    /// </summary>
    public class UpdateSpecialOccasionDto
    {
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string? Name { get; set; }

        [MaxLength(50, ErrorMessage = "نوع مناسبت نامعتبر است")]
        public string? Type { get; set; }

        [MaxLength(30, ErrorMessage = "دسته‌بندی مناسبت نامعتبر است")]
        public string? Category { get; set; }

        [MaxLength(20, ErrorMessage = "نوع تقویم نامعتبر است")]
        public string? CalendarType { get; set; }

        [Range(1, 12, ErrorMessage = "ماه باید بین ۱ تا ۱۲ باشد")]
        public int? Month { get; set; }

        [Range(1, 31, ErrorMessage = "روز باید بین ۱ تا ۳۱ باشد")]
        public int? Day { get; set; }

        public DateTime? OccasionDate { get; set; }

        [MaxLength(2000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد")]
        public string? DefaultMessage { get; set; }

        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO نمایش ساده مناسبت (سازگاری با API قبلی)
    /// </summary>
    public class SpecialOccasionResponseDto
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
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// ردیف جدول تبریک/تسلیت مناسبتی برای کاربر
    /// </summary>
    public class OccasionTableItemDto
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
        public int DaysRemaining { get; set; }
        public bool IsToday { get; set; }
        public bool IsSystem { get; set; }

        /// <summary>فعال بودن این مناسبت برای کاربر</summary>
        public bool IsEnabled { get; set; }

        /// <summary>متن مؤثر قالب (سفارشی تأییدشده یا پیش‌فرض ادمین)</summary>
        public string? EffectiveMessage { get; set; }

        /// <summary>متن پیش‌فرض ادمین</summary>
        public string? DefaultMessage { get; set; }

        /// <summary>متن سفارشی کاربر (ممکن است در انتظار تأیید باشد)</summary>
        public string? CustomMessage { get; set; }

        public string TemplateApprovalStatus { get; set; } = AdminApprovalStatuses.Approved;
        public string? TemplateRejectionReason { get; set; }
        public int? MessageTemplateId { get; set; }
        public bool CanSendWithCurrentTemplate { get; set; }
    }

    /// <summary>
    /// پاسخ کامل جدول مناسبتی کاربر
    /// </summary>
    public class OccasionTableResponseDto
    {
        public UserOccasionProfileDto Profile { get; set; } = new();
        public List<OccasionTableItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int EnabledCount { get; set; }
        public OccasionCalendarTodayDto Today { get; set; } = new();
    }

    public class OccasionCalendarTodayDto
    {
        public string TehranDate { get; set; } = string.Empty;
        public int JalaliYear { get; set; }
        public int JalaliMonth { get; set; }
        public int JalaliDay { get; set; }
        public int GregorianYear { get; set; }
        public int GregorianMonth { get; set; }
        public int GregorianDay { get; set; }
        public int HijriYear { get; set; }
        public int HijriMonth { get; set; }
        public int HijriDay { get; set; }
    }

    public class UserOccasionProfileDto
    {
        public string? BusinessName { get; set; }
        public bool CongratulationsEnabled { get; set; } = true;
        public bool CondolencesEnabled { get; set; } = true;
        public string? ScheduledTimeTehran { get; set; }
        public int? AutomatedMessageId { get; set; }
    }

    public class UpdateUserOccasionProfileDto
    {
        [MaxLength(200, ErrorMessage = "نام کسب‌وکار نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string? BusinessName { get; set; }

        public bool? CongratulationsEnabled { get; set; }

        public bool? CondolencesEnabled { get; set; }

        /// <summary>ساعت ارسال به وقت تهران — HH:mm</summary>
        [RegularExpression(@"^([01]?\d|2[0-3]):[0-5]\d$", ErrorMessage = "فرمت ساعت نامعتبر است. باید به صورت HH:mm باشد")]
        public string? ScheduledTimeTehran { get; set; }

        public int? AutomatedMessageId { get; set; }
    }

    public class ToggleOccasionPreferenceDto
    {
        [Required(ErrorMessage = "وضعیت فعال بودن الزامی است")]
        public bool IsEnabled { get; set; }
    }

    public class UpdateOccasionTemplateDto
    {
        [Required(ErrorMessage = "متن قالب الزامی است")]
        [MaxLength(2000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد")]
        public string CustomMessage { get; set; } = string.Empty;
    }

    public class ToggleOccasionCategoryDto
    {
        [Required(ErrorMessage = "دسته‌بندی الزامی است")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "وضعیت فعال بودن الزامی است")]
        public bool IsEnabled { get; set; }
    }
}
