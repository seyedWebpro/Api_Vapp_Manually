namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// درخواست ثبت ارسال پیامک برای پیگیری دلیوری
    /// </summary>
    public class SmsDeliveryTrackRequestDto
    {
        public int UserId { get; set; }
        public string SourceModule { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public string? SourceEntityLabel { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public long Sid { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
