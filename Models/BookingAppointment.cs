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
        /// یادداشت مشتری / توضیحات رزرو — اختیاری
        /// </summary>
        public string? CustomerNote { get; set; }

        /// <summary>
        /// مسیر نسبی فیش واریز (اختیاری) — فقط برای خدمات هزینه‌دار
        /// </summary>
        public string? PaymentReceiptPath { get; set; }

        /// <summary>
        /// مخاطب ذخیره‌شده در دفترچه — در صورت فعال بودن SaveToPhonebook
        /// </summary>
        public int? ContactId { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        /// <summary>
        /// Pending | Confirmed | Cancelled | Completed
        /// </summary>
        public string Status { get; set; } = BookingAppointmentStatuses.Pending;

        public DateTime? ReminderSentAt { get; set; }

        /// <summary>
        /// آیا مشتری مایل به دریافت پیامک یادآوری است (پیش‌فرض true).
        /// </summary>
        public bool RemindersEnabled { get; set; } = true;

        /// <summary>
        /// Offsetهایی که برای این نوبت ارسال شده‌اند — CSV مثل "60,1440".
        /// </summary>
        public string? ReminderSentOffsetsCsv { get; set; }

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
        /// <summary>منتظر تأیید مالک</summary>
        public const string Pending = "Pending";

        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string Completed = "Completed";

        /// <summary>نوبت‌هایی که اسلات را اشغال می‌کنند</summary>
        public static bool IsActive(string status) =>
            status == Pending || status == Confirmed;

        public static bool IsValid(string status) =>
            status is Pending or Confirmed or Cancelled or Completed;
    }
}
