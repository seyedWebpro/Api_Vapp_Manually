namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای نتیجه ایمپورت مخاطبین از اکسل
    /// </summary>
    public class ImportExcelResultDto
    {
        /// <summary>
        /// تعداد کل ردیف‌های پردازش شده
        /// </summary>
        public int TotalRows { get; set; }

        /// <summary>
        /// تعداد مخاطبین ایجاد شده با موفقیت
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// تعداد ردیف‌های تکراری (شماره موبایل تکراری در دفترچه)
        /// </summary>
        public int DuplicateCount { get; set; }

        /// <summary>
        /// تعداد ردیف‌های با خطا
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// تعداد ردیف‌های رد شده (شماره موبایل نامعتبر یا خالی)
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// لیست خطاها با شماره ردیف
        /// </summary>
        public List<ImportRowError> Errors { get; set; } = new List<ImportRowError>();

        /// <summary>
        /// مسیر فایل آپلود شده
        /// </summary>
        public string? UploadedFilePath { get; set; }
    }

    /// <summary>
    /// خطای ردیف در ایمپورت اکسل
    /// </summary>
    public class ImportRowError
    {
        /// <summary>
        /// شماره ردیف در اکسل
        /// </summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// شماره موبایل (در صورت وجود)
        /// </summary>
        public string? MobileNumber { get; set; }

        /// <summary>
        /// پیام خطا
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

