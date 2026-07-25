namespace Api_Vapp.Models
{
    /// <summary>
    /// اسلات مسدودشده توسط مالک در «مدیریت وقت خالی»
    /// </summary>
    public class BookingSlotBlock
    {
        public int Id { get; set; }

        public int BookingSystemId { get; set; }

        /// <summary>
        /// شروع اسلات مسدود — UTC
        /// </summary>
        public DateTime SlotStartUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual BookingSystem BookingSystem { get; set; } = null!;
    }
}
