namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ دریافت وضعیت پیامک (Delivery)
    /// </summary>
    public class DeliveryResponseDto
    {
        public List<DeliveryItemDto> Deliveries { get; set; } = new List<DeliveryItemDto>();
        public string Messege { get; set; } = string.Empty; // Note: API uses "Messege" not "Message"
        public int Status { get; set; }
    }

    /// <summary>
    /// DTO برای هر آیتم دلیوری
    /// </summary>
    public class DeliveryItemDto
    {
        public string Mobile { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusMessage { get; set; } = string.Empty; // متن فارسی وضعیت
    }
}



