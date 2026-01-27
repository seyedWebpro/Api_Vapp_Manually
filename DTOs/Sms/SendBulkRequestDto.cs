namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای درخواست ارسال پیامک گروهی (Bulk)
    /// </summary>
    public class SendBulkRequestDto
    {
        public string SenderNumber { get; set; } = string.Empty;
        public List<string> Mobiles { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
    }
}



