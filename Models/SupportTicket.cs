namespace Api_Vapp.Models
{
    /// <summary>
    /// تیکت پشتیبانی
    /// </summary>
    public class SupportTicket
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Normal";
        public int? AssignedToUserId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual User? AssignedToUser { get; set; }
        public virtual ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    }
}
