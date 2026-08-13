using Api_Vapp.DTOs.Common;

namespace Api_Vapp.DTOs.UserForm
{
    public class UserFormListResponseDto
    {
        public PagedResponse<UserFormSummaryDto> Forms { get; set; } = PagedResponse<UserFormSummaryDto>.Create(
            Array.Empty<UserFormSummaryDto>(), 0, 1, 10);
    }

    public class UserFormSummaryDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? PublicUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        /// <summary>Pending / Approved / Rejected — تأیید یک‌باره ارسال سریع</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public string? RejectionReason { get; set; }

        public DateTime? ApprovedAt { get; set; }
    }
}
