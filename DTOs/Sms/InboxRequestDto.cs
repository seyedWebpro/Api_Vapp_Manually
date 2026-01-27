namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای درخواست دریافت پیامک‌های ورودی (Inbox)
    /// </summary>
    public class InboxRequestDto
    {
        public string SenderNumber { get; set; } = string.Empty;
        public int Count { get; set; } = 100; // حداکثر 100
    }
}



