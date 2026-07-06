using Api_Vapp.DTOs.Common;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class LuckyWheelListResponseDto
    {
        public PagedResponse<LuckyWheelSummaryDto> Wheels { get; set; } = PagedResponse<LuckyWheelSummaryDto>.Create(
            Array.Empty<LuckyWheelSummaryDto>(), 0, 1, 10);
    }

    public class LuckyWheelSummaryDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? PublicUrl { get; set; }

        /// <summary>
        /// تعداد شرکت‌کنندگان — فاز ۲ (چرخش عمومی) پر می‌شود؛ فعلاً ۰
        /// </summary>
        public int ParticipantCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }
    }
}
