using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Notification
{
    public class InAppNotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ActionUrl { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public string? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UnreadNotificationCountDto
    {
        public int Count { get; set; }
    }

    public class MarkNotificationReadDto
    {
        [Required(ErrorMessage = "شناسه اعلان الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "شناسه اعلان نامعتبر است")]
        public int NotificationId { get; set; }
    }
}
