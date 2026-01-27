namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای نتیجه اکسپورت مخاطبین به اکسل
    /// </summary>
    public class ExportExcelResultDto
    {
        /// <summary>
        /// محتوای فایل اکسل (byte array)
        /// </summary>
        public byte[] FileContent { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// نام فایل
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// نوع محتوا (Content-Type)
        /// </summary>
        public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>
        /// تعداد کل مخاطبین در دفترچه
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// تعداد مخاطبین اکسپورت شده در این صفحه
        /// </summary>
        public int ExportedCount { get; set; }

        /// <summary>
        /// شماره صفحه
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// تعداد آیتم در هر صفحه
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// تعداد کل صفحات
        /// </summary>
        public int TotalPages { get; set; }
    }
}

