namespace Api_Vapp.Models
{
    /// <summary>
    /// شرکت‌کننده در چرخش عمومی گردونه شانس
    /// </summary>
    public class LuckyWheelParticipant
    {
        public int Id { get; set; }

        public int LuckyWheelId { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        public int WonLuckyWheelItemId { get; set; }

        /// <summary>
        /// کد یکتای جایزه برای راستی‌آزمایی (مثلاً LW-A3K9P2)
        /// </summary>
        public string PrizeCode { get; set; } = string.Empty;

        /// <summary>
        /// مخاطب ذخیره‌شده در دفترچه — در صورت فعال بودن SaveToPhonebook
        /// </summary>
        public int? ContactId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual LuckyWheel LuckyWheel { get; set; } = null!;

        public virtual LuckyWheelItem WonItem { get; set; } = null!;

        public virtual Contact? Contact { get; set; }
    }
}
