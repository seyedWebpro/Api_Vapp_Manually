namespace Api_Vapp.Models
{
    /// <summary>
    /// بانک آماده شماره برای شماره‌جو (مدل فقط‌بانک) — اسکرپ جدا، تحویل به کاربر از این جدول.
    /// </summary>
    public class NumberSeekerPhoneBank
    {
        public int Id { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>منبع: balad, divar, sheypoor, nshan, googlemaps, …</summary>
        public string Source { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        /// <summary>آماده‌ی تحویل به کاربر</summary>
        public bool IsAvailable { get; set; } = true;

        public int ServedCount { get; set; }

        public DateTime? LastServedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
