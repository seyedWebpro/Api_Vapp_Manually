namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ ارسال پیامک
    /// </summary>
    public class SendSmsResponseDto
    {
        public long Sid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Status { get; set; }
    }
}


