namespace Api_Vapp.DTOs.Admin
{
    /// <summary>
    /// خلاصه دارایی‌های یک کاربر برای هاب ادمین
    /// </summary>
    public class AdminUserInventorySummaryDto
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int TemplatesCount { get; set; }
        public int ContentsCount { get; set; }
        public int BusinessCardsCount { get; set; }
        public int UserFormsCount { get; set; }
        public int LuckyWheelsCount { get; set; }
        public int BookingSystemsCount { get; set; }
        public int SocialMediaLinksCount { get; set; }
        public int QuickActionsCount { get; set; }
        public int BankAccountsCount { get; set; }
    }

    /// <summary>
    /// آیتم قالب در هاب کاربر — محتوا اینجا برنمی‌گردد؛ فقط لینک مشاهده
    /// </summary>
    public class AdminUserTemplateItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// مسیر نسبی پنل ادمین برای مشاهده قالب (صف تأیید با فیلتر کاربر)
        /// </summary>
        public string AdminViewPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// آیتم محتوا / ارسال سریع متعلق به کاربر
    /// </summary>
    public class AdminUserContentItemDto
    {
        public string ItemType { get; set; } = string.Empty;
        public string ItemTypeTitle { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>Draft / Published — فقط برای انواع لینک‌دار</summary>
        public string? PublishStatus { get; set; }

        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }

        /// <summary>لینک عمومی کامل در صورت وجود slug (ممکن است برای عموم مسدود باشد)</summary>
        public string? PublicUrl { get; set; }

        /// <summary>پیش‌نمایش متنی (مثلاً شماره حساب / اقدام سریع / کپشن SMS)</summary>
        public string? ContentPreview { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>آیا ادمین می‌تواند لینک مشاهده بگیرد</summary>
        public bool CanView { get; set; }

        /// <summary>
        /// Public | AdminPreview | External | AdminPage
        /// </summary>
        public string? ViewMode { get; set; }
    }

    /// <summary>
    /// نتیجه ساخت لینک مشاهده برای ادمین
    /// </summary>
    public class AdminUserContentViewLinkDto
    {
        public string ItemType { get; set; } = string.Empty;
        public int Id { get; set; }

        /// <summary>Public | AdminPreview | External | AdminPage</summary>
        public string ViewMode { get; set; } = string.Empty;

        /// <summary>URL کامل برای Public / External</summary>
        public string? Url { get; set; }

        /// <summary>مسیر نسبی Public_Vapp برای AdminPreview — مثلاً /preview/{token}</summary>
        public string? PreviewPath { get; set; }

        /// <summary>مسیر نسبی پنل ادمین برای AdminPage</summary>
        public string? AdminPath { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>حالت‌های مشاهده محتوا توسط ادمین</summary>
    public static class AdminContentViewModes
    {
        public const string Public = "Public";
        public const string AdminPreview = "AdminPreview";
        public const string External = "External";
        public const string AdminPage = "AdminPage";
    }
}
