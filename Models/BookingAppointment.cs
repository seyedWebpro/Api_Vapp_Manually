namespace Api_Vapp.Models
{
    /// <summary>
    /// نوبت رزرو شده توسط مشتری
    /// </summary>
    public class BookingAppointment
    {
        public int Id { get; set; }

        public int BookingSystemId { get; set; }

        public int BookingServiceItemId { get; set; }

        public string CustomerFullName { get; set; } = string.Empty;

        public string CustomerMobile { get; set; } = string.Empty;

        /// <summary>
        /// مخاطب ذخیره‌شده در دفترچه — در صورت فعال بودن SaveToPhonebook
        /// </summary>
        public int? ContactId { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        /// <summary>
        /// Confirmed | Cancelled | Completed
        /// </summary>
        public string Status { get; set; } = BookingAppointmentStatuses.Confirmed;

        public DateTime? ReminderSentAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string? CancellationReason { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual BookingSystem BookingSystem { get; set; } = null!;

        public virtual BookingServiceItem BookingServiceItem { get; set; } = null!;

        public virtual Contact? Contact { get; set; }
    }

    public static class BookingAppointmentStatuses
    {
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string Completed = "Completed";

        public static bool IsActive(string status) =>
            status == Confirmed;
    }
}
