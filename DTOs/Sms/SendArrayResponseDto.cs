namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ ارسال پیامک نظیر به نظیر (Array)
    /// </summary>
    public class SendArrayResponseDto
    {
        public long Sid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Status { get; set; }
    }
}



