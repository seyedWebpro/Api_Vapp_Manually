using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Admin
{
    public class TicketMessageResponseDto
    {
        public int Id { get; set; }
        public int SenderUserId { get; set; }
        public string? SenderName { get; set; }
        public bool IsAdminReply { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SupportTicketResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserFullName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int ReplyCount { get; set; }
        public List<TicketMessageResponseDto> Messages { get; set; } = new();
    }

    public class CreateSupportTicketDto
    {
        [Required]
        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Priority { get; set; } = "Normal";
    }

    public class ReplySupportTicketDto
    {
        [MaxLength(4000)]
        public string Content { get; set; } = string.Empty;
    }

    public class ReplySupportTicketFormDto
    {
        [MaxLength(4000)]
        public string? Content { get; set; }

        public IFormFile? ImageFile { get; set; }
    }

    public class UpdateSupportTicketStatusDto
    {
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        public int? AssignedToUserId { get; set; }
    }
}
