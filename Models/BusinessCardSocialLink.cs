namespace Api_Vapp.Models
{
    /// <summary>
    /// لینک شبکه اجتماعی کارت ویزیت — امکان چند لینک از یک نوع با برچسب سفارشی
    /// </summary>
    public class BusinessCardSocialLink
    {
        public int Id { get; set; }

        public int BusinessCardId { get; set; }

        /// <summary>
        /// نوع شبکه (مثلاً instagram، telegram، whatsapp، eitaa، rubika، bale، …)
        /// </summary>
        public string NetworkType { get; set; } = string.Empty;

        /// <summary>
        /// نام نمایشی سفارشی (مثلاً «اینستاگرام کاری») — اختیاری
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// هندل، شماره یا URL
        /// </summary>
        public string Value { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public virtual BusinessCard BusinessCard { get; set; } = null!;
    }
}
