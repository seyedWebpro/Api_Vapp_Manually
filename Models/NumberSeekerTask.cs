namespace Api_Vapp.Models
{
    /// <summary>
    /// نگاشت تسک اسکرپ ربات به کاربر Vapp — برای امنیت و تاریخچه.
    /// </summary>
    public class NumberSeekerTask
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>شناسه UUID تسک در سرویس Python</summary>
        public string ScraperTaskId { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int TargetCount { get; set; }

        public string Status { get; set; } = "pending";

        public int CurrentCount { get; set; }

        public string? ResultCode { get; set; }

        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        /// <summary>زمان import به دفترچه تلفن</summary>
        public DateTime? ImportedAt { get; set; }

        /// <summary>تعداد شماره‌های import شده به Contact</summary>
        public int ImportedCount { get; set; }

        /// <summary>دفترچه مقصد import</summary>
        public int? ImportedNotebookId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
