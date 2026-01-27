namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای لیست کمپین‌ها با Pagination
    /// </summary>
    public class CampaignListResponseDto
    {
        public List<CampaignResponseDto> Campaigns { get; set; } = new List<CampaignResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}


