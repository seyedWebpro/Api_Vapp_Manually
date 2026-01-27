namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل ارتباط بین مخاطب و تگ
    /// Many-to-Many relationship
    /// </summary>
    public class ContactTag
    {
        // شناسه یکتای ارتباط
        public int Id { get; set; }

        // شناسه مخاطب
        public int ContactId { get; set; }

        // شناسه تگ
        public int TagId { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Navigation Properties

        // مخاطب
        public virtual Contact Contact { get; set; } = null!;

        // تگ
        public virtual MessageTag Tag { get; set; } = null!;

        #endregion
    }
}


