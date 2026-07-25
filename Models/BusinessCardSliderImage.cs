namespace Api_Vapp.Models
{
    /// <summary>
    /// تصویر اسلایدر کارت ویزیت
    /// </summary>
    public class BusinessCardSliderImage
    {
        public int Id { get; set; }

        public int BusinessCardId { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public virtual BusinessCard BusinessCard { get; set; } = null!;
    }
}
