namespace Api_Vapp.Models
{
    /// <summary>
    /// ارتباط گردونه با دفترچه‌های تلفن برای ذخیره شرکت‌کنندگان
    /// </summary>
    public class LuckyWheelNotebook
    {
        public int LuckyWheelId { get; set; }

        public int ContactNotebookId { get; set; }

        public virtual LuckyWheel LuckyWheel { get; set; } = null!;

        public virtual ContactNotebook ContactNotebook { get; set; } = null!;
    }
}
