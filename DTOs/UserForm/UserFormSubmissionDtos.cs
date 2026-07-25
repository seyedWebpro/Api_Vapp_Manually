using Api_Vapp.DTOs.Common;

namespace Api_Vapp.DTOs.UserForm
{
    /// <summary>
    /// صفحه پاسخ‌های ثبت‌شده یک فرم (لیست صاحب فرم)
    /// </summary>
    public class UserFormSubmissionsPageDto
    {
        public string FormTitle { get; set; } = string.Empty;

        public int SubmissionCount { get; set; }

        public PagedResponse<UserFormSubmissionListItemDto> Submissions { get; set; } =
            PagedResponse<UserFormSubmissionListItemDto>.Create(
                Array.Empty<UserFormSubmissionListItemDto>(), 0, 1, 10);
    }

    public class UserFormSubmissionListItemDto
    {
        public int Id { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        /// <summary>
        /// ایمیل از فیلد فرم (معمولاً fieldKey=email) — در صورت نبود، خالی
        /// </summary>
        public string ParticipantEmail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
