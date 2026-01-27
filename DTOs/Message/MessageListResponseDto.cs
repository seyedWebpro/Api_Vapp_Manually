namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای لیست پیام‌ها با Pagination
    /// </summary>
    public class MessageListResponseDto
    {
        public List<MessageResponseDto> Messages { get; set; } = new List<MessageResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}


