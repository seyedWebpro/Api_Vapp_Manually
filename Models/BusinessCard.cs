namespace Api_Vapp.Models
{
    /// <summary>
    /// کارت ویزیت دیجیتال ساخته‌شده توسط کاربر
    /// </summary>
    public class BusinessCard : IQuickSendApprovable
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>
        /// نام کسب‌وکار
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// مسیر نسبی لوگو / پروفایل
        /// </summary>
        public string? LogoUrl { get; set; }

        /// <summary>
        /// شناسه URL عمومی — پس از publish تنظیم می‌شود
        /// </summary>
        public string? Slug { get; set; }

        /// <summary>
        /// کلید قالب از سمت کلاینت (مثلاً business / classic)
        /// </summary>
        public string? TemplateKey { get; set; }

        /// <summary>
        /// شناسه قالب سمت سرور — فعلاً null
        /// </summary>
        public int? TemplateId { get; set; }

        public BusinessCardStatus Status { get; set; } = BusinessCardStatus.Draft;

        /// <summary>
        /// برای کارت‌های منتشرشده — غیرفعال = لینک عمومی کار نمی‌کند
        /// </summary>
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        /// <summary>وضعیت تأیید ادمین برای ارسال سریع (Pending / Approved / Rejected)</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        public string? RejectionReason { get; set; }

        public bool SliderEnabled { get; set; }

        public bool DescriptionEnabled { get; set; } = true;

        public bool ServicesEnabled { get; set; }

        public bool MapEnabled { get; set; }

        public bool ContactEnabled { get; set; } = true;

        /// <summary>
        /// بخش اطلاعات بانکی (شماره حساب / کارت / شبا)
        /// </summary>
        public bool BankingEnabled { get; set; }

        public string? DescriptionTitle { get; set; }

        public string? DescriptionText { get; set; }

        public double? MapLatitude { get; set; }

        public double? MapLongitude { get; set; }

        public string? MapAddress { get; set; }

        public string? ContactPhone { get; set; }

        public string? ContactEmail { get; set; }

        /// <summary>
        /// فیلد قدیمی اینستاگرام — برای سازگاری با کلاینت‌های قبلی نگه داشته می‌شود
        /// و از اولین لینک Instagram در SocialLinks همگام می‌شود.
        /// </summary>
        public string? ContactInstagram { get; set; }

        /// <summary>
        /// شماره حساب بانکی
        /// </summary>
        public string? BankAccountNumber { get; set; }

        /// <summary>
        /// شماره کارت ۱۶ رقمی
        /// </summary>
        public string? BankCardNumber { get; set; }

        /// <summary>
        /// شماره شبا (IR + ۲۴ رقم)
        /// </summary>
        public string? BankShebaNumber { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual ICollection<BusinessCardSliderImage> SliderImages { get; set; } = new List<BusinessCardSliderImage>();

        public virtual ICollection<BusinessCardServiceItem> ServiceItems { get; set; } = new List<BusinessCardServiceItem>();

        public virtual ICollection<BusinessCardSocialLink> SocialLinks { get; set; } = new List<BusinessCardSocialLink>();
    }
}
