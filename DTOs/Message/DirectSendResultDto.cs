namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای نتیجه ارسال مستقیم پیام
    /// </summary>
    public class DirectSendResultDto
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public decimal TotalCost { get; set; }
        public List<string>? FailedNumbers { get; set; }
    }
}

