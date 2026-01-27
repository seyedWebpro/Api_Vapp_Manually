namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای نمایش تگ پیام
    /// </summary>
    public class MessageTagResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO برای نمایش تگ با تعداد مخاطبین
    /// </summary>
    public class MessageTagWithContactCountDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ContactCount { get; set; } // تعداد مخاطبین دارای این تگ
    }

    /// <summary>
    /// DTO برای لیست تگ‌ها
    /// </summary>
    public class MessageTagListResponseDto
    {
        public List<MessageTagResponseDto> Tags { get; set; } = new List<MessageTagResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// DTO برای لیست تگ‌ها با تعداد مخاطبین
    /// </summary>
    public class MessageTagWithContactCountListResponseDto
    {
        public List<MessageTagWithContactCountDto> Tags { get; set; } = new List<MessageTagWithContactCountDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}



