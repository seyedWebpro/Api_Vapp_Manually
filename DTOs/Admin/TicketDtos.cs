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

        /// <summary>
        /// شماره نمایشی تیکت مثل TK-1024
        /// </summary>
        public string TicketNumber { get; set; } = string.Empty;

        public int UserId { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserFullName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string ModuleFa { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusFa { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string PriorityFa { get; set; } = string.Empty;
        public int? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int ReplyCount { get; set; }
        public List<TicketMessageResponseDto> Messages { get; set; } = new();
    }

    public class SupportTicketStatsDto
    {
        public int TotalCount { get; set; }
        public int WaitingForResponseCount { get; set; }
        public int AnsweredCount { get; set; }
        public int ClosedCount { get; set; }
    }

    public class TicketModuleOptionDto
    {
        public string Code { get; set; } = string.Empty;
        public string TitleFa { get; set; } = string.Empty;
    }

    public class CreateSupportTicketDto
    {
        [Required(ErrorMessage = "موضوع تیکت الزامی است")]
        [MaxLength(300, ErrorMessage = "موضوع نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "ماژول الزامی است")]
        [MaxLength(50, ErrorMessage = "ماژول نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string Module { get; set; } = "Other";

        [Required(ErrorMessage = "شرح درخواست الزامی است")]
        [MaxLength(4000, ErrorMessage = "شرح درخواست نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "اولویت نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string Priority { get; set; } = "Normal";
    }

    public class CreateSupportTicketFormDto
    {
        [Required(ErrorMessage = "موضوع تیکت الزامی است")]
        [MaxLength(300, ErrorMessage = "موضوع نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "ماژول الزامی است")]
        [MaxLength(50, ErrorMessage = "ماژول نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string Module { get; set; } = "Other";

        [Required(ErrorMessage = "شرح درخواست الزامی است")]
        [MaxLength(4000, ErrorMessage = "شرح درخواست نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "اولویت نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string Priority { get; set; } = "Normal";

        public IFormFile? AttachmentFile { get; set; }
    }

    public class ReplySupportTicketDto
    {
        [MaxLength(4000, ErrorMessage = "متن پیام نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد")]
        public string Content { get; set; } = string.Empty;
    }

    public class ReplySupportTicketFormDto
    {
        [MaxLength(4000, ErrorMessage = "متن پیام نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد")]
        public string? Content { get; set; }

        public IFormFile? AttachmentFile { get; set; }

        /// <summary>
        /// سازگاری با کلاینت ادمین فعلی که imageFile می‌فرستد
        /// </summary>
        public IFormFile? ImageFile { get; set; }

        public IFormFile? GetAttachment() =>
            AttachmentFile is { Length: > 0 } ? AttachmentFile
            : ImageFile is { Length: > 0 } ? ImageFile
            : null;
    }

    public class UpdateSupportTicketStatusDto
    {
        [Required(ErrorMessage = "وضعیت تیکت الزامی است")]
        [MaxLength(50, ErrorMessage = "وضعیت نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string Status { get; set; } = string.Empty;

        public int? AssignedToUserId { get; set; }
    }
}
