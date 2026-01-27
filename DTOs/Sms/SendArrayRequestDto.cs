namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای درخواست ارسال پیامک نظیر به نظیر (هر شماره متن خودش)
    /// </summary>
    public class SendArrayRequestDto
    {
        public string SenderNumber { get; set; } = string.Empty;
        public List<string> Mobiles { get; set; } = new List<string>();
        public List<string> Message { get; set; } = new List<string>(); // باید هم‌طول با Mobiles باشد
    }
}



