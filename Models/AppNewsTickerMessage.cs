namespace Api_Vapp.Models
{
    /// <summary>متن زیرنویس خبری قابل نمایش در اپ موبایل.</summary>
    public class AppNewsTickerMessage
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
