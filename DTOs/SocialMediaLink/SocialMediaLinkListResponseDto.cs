namespace Api_Vapp.DTOs.SocialMediaLink
{
    /// <summary>
    /// DTO برای لیست لینک‌های شبکه‌های اجتماعی با pagination
    /// </summary>
    public class SocialMediaLinkListResponseDto
    {
        public List<SocialMediaLinkResponseDto> SocialMediaLinks { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}





