using Api_Vapp.DTOs.Common;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class LuckyWheelParticipantsPageDto
    {
        public string WheelTitle { get; set; } = string.Empty;

        public int ParticipantCount { get; set; }

        /// <summary>
        /// فعلاً برابر تعداد شرکت‌کنندگان است (هر چرخش یک جایزه دارد)
        /// </summary>
        public int PrizeAwardedCount { get; set; }

        public PagedResponse<LuckyWheelParticipantListItemDto> Participants { get; set; } =
            PagedResponse<LuckyWheelParticipantListItemDto>.Create(
                Array.Empty<LuckyWheelParticipantListItemDto>(), 0, 1, 10);
    }

    public class LuckyWheelParticipantListItemDto
    {
        public int Id { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int WonItemId { get; set; }

        public string WonItemName { get; set; } = string.Empty;

        public string PrizeCode { get; set; } = string.Empty;
    }

    public class LuckyWheelParticipantVerifyDto
    {
        public int Id { get; set; }

        public string WheelTitle { get; set; } = string.Empty;

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int WonItemId { get; set; }

        public string WonItemName { get; set; } = string.Empty;

        public string PrizeCode { get; set; } = string.Empty;

        public int? ContactId { get; set; }
    }
}
