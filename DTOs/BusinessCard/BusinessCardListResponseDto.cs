using Api_Vapp.DTOs.Common;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class BusinessCardListResponseDto
    {
        public PagedResponse<BusinessCardSummaryDto> Cards { get; set; } = PagedResponse<BusinessCardSummaryDto>.Create(
            Array.Empty<BusinessCardSummaryDto>(), 0, 1, 10);
    }

    public class BusinessCardSummaryDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        public string? Slug { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? PublicUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }
    }
}
