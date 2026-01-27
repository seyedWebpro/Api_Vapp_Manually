namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای آمار دفترچه تلفن
    /// </summary>
    public class ContactNotebookStatisticsDto
    {
        /// <summary>
        /// تعداد فایل‌های اکسل ایمپورت شده
        /// </summary>
        public int ImportedFilesCount { get; set; }

        /// <summary>
        /// تاریخ آخرین بروزرسانی (به صورت نسبی مانند "24 ساعت پیش")
        /// </summary>
        public string? LastUpdateRelativeTime { get; set; }

        /// <summary>
        /// تاریخ آخرین بروزرسانی (DateTime)
        /// </summary>
        public DateTime? LastUpdateDateTime { get; set; }

        /// <summary>
        /// تعداد کل مخاطبین
        /// </summary>
        public int ContactsCount { get; set; }
    }
}


