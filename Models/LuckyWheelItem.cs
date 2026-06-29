namespace Api_Vapp.Models
{
    /// <summary>
    /// آیتم/جایزه گردونه شانس
    /// </summary>
    public class LuckyWheelItem
    {
        public int Id { get; set; }

        public int LuckyWheelId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// درصد شانس برد (۰ تا ۱۰۰) — مجموع آیتم‌ها باید ۱۰۰ باشد
        /// </summary>
        public decimal Probability { get; set; }

        public int DisplayOrder { get; set; }

        public virtual LuckyWheel LuckyWheel { get; set; } = null!;
    }
}
