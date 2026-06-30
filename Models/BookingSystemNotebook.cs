namespace Api_Vapp.Models
{
    /// <summary>
    /// ارتباط سیستم رزرو با دفترچه‌های تلفن برای ذخیره رزروکنندگان
    /// </summary>
    public class BookingSystemNotebook
    {
        public int BookingSystemId { get; set; }

        public int ContactNotebookId { get; set; }

        public virtual BookingSystem BookingSystem { get; set; } = null!;

        public virtual ContactNotebook ContactNotebook { get; set; } = null!;
    }
}
