namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ ارسال پیامک گروهی (Bulk)
    /// </summary>
    public class SendBulkResponseDto
    {
        public long Sid { get; set; }
        public string Messege { get; set; } = string.Empty; // Note: API uses "Messege" not "Message"
        public int Status { get; set; }
        public List<string>? BlackList { get; set; } // شماره‌های بلک‌لیست (اختیاری)
    }
}



