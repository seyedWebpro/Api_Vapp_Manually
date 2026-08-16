namespace Api_Vapp.Models
{
    /// <summary>
    /// سیاست نسخه اپ برای هر پلتفرم (android / ios).
    /// یک ردیف فعال به‌ازای هر پلتفرم از seed می‌آید و از پنل ادمین به‌روز می‌شود.
    /// </summary>
    public class AppVersionPolicy
    {
        public int Id { get; set; }

        /// <summary>android | ios</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>آخرین نسخه منتشرشده (مثلاً 1.1.0)</summary>
        public string LatestVersion { get; set; } = "1.1.0";

        /// <summary>
        /// حداقل نسخه پشتیبانی‌شده.
        /// اگر اپ کمتر از این باشد → forced؛ در غیر این صورت اگر کمتر از Latest باشد → optional.
        /// برای آپدیت اختیاری روی 1.0.0 نگه داشته می‌شود.
        /// </summary>
        public string MinSupportedVersion { get; set; } = "1.0.0";

        public string? StoreUrl { get; set; }

        public string? Title { get; set; }

        public string? Message { get; set; }

        /// <summary>JSON آرایه رشته‌ها، مثلاً ["رفع باگ"]</summary>
        public string? ChangelogJson { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
