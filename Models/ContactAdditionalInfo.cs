    namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل اطلاعات تکمیلی مخاطب
    /// شامل تاریخ تولد، تاریخ ازدواج و سایر فیلدهای سفارشی
    /// </summary>
    public class ContactAdditionalInfo
    {
        // شناسه یکتای اطلاعات تکمیلی
        public int Id { get; set; }

        // شناسه مخاطب
        public int ContactId { get; set; }

        // تاریخ تولد
        public DateTime? DateOfBirth { get; set; }

        // تاریخ ازدواج
        public DateTime? MarriageDate { get; set; }

        // سایر اطلاعات سفارشی (به صورت JSON)
        public string? CustomFields { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // مخاطب مربوطه
        public virtual Contact Contact { get; set; } = null!;

        #endregion
    }
}


