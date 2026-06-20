namespace Api_Vapp.Models
{
    /// <summary>
    /// پیام داخل تیکت پشتیبانی
    /// </summary>
    public class TicketMessage
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public int SenderUserId { get; set; }
        public bool IsAdminReply { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual SupportTicket Ticket { get; set; } = null!;
        public virtual User SenderUser { get; set; } = null!;
    }
}
