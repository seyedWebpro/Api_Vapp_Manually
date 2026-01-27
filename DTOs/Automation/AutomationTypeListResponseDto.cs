namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای لیست انواع اتوماسیون با Pagination
    /// </summary>
    public class AutomationTypeListResponseDto
    {
        public List<AutomationTypeDto> Types { get; set; } = new List<AutomationTypeDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

