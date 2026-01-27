namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای درخواست ارسال پیامک تکی
    /// </summary>
    public class SendSmsRequestDto
    {
        public string SenderNumber { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        
        // فیلدهای اختیاری برای Template (اگر API از Template استفاده کند)
        public string? TemplateId { get; set; }
        public string? Parameter1 { get; set; } // برای کد OTP
        public string? Parameter2 { get; set; }
        public string? Parameter3 { get; set; }
    }
}

