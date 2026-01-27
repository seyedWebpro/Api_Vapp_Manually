namespace Api_Vapp.DTOs.QuickAction
{
    /// <summary>
    /// DTO برای لیست اکشن‌ها با pagination
    /// </summary>
    public class QuickActionListResponseDto
    {
        public List<QuickActionResponseDto> QuickActions { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

