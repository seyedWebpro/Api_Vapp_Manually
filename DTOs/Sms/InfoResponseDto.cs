namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ دریافت موجودی کیف پول (Info)
    /// </summary>
    public class InfoResponseDto
    {
        public decimal WalletBalance { get; set; }
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}



