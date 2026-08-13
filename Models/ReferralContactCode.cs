namespace Api_Vapp.Models
{
    /// <summary>
    /// کد معرف شخصی هر مخاطب در یک برنامه پاداش
    /// </summary>
    public class ReferralContactCode
    {
        public int Id { get; set; }

        public int ReferralProgramId { get; set; }

        /// <summary>
        /// صاحب فروشگاه / سازنده برنامه
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// مخاطب معرف (صاحب کد)
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// کد یکتای معرف برای این مخاطب (مثلاً REF482931)
        /// </summary>
        public string Code { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ReferralProgram ReferralProgram { get; set; } = null!;

        public virtual User User { get; set; } = null!;

        public virtual Contact Contact { get; set; } = null!;
    }
}
