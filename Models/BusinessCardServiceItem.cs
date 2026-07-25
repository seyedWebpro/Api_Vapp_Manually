namespace Api_Vapp.Models
{
    /// <summary>
    /// تعرفه / خدمت کارت ویزیت
    /// </summary>
    public class BusinessCardServiceItem
    {
        public int Id { get; set; }

        public int BusinessCardId { get; set; }

        public string Title { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; }

        public virtual BusinessCard BusinessCard { get; set; } = null!;
    }
}
