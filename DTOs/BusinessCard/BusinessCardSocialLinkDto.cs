using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    /// <summary>
    /// لینک شبکه اجتماعی کارت ویزیت
    /// </summary>
    public class BusinessCardSocialLinkDto
    {
        public int? Id { get; set; }

        /// <summary>
        /// نوع شبکه: instagram, telegram, whatsapp, linkedin, twitter, youtube,
        /// facebook, tiktok, snapchat, rubika, soroush, eitaa, bale, website, custom
        /// </summary>
        [Required(ErrorMessage = "نوع شبکه اجتماعی الزامی است")]
        [MaxLength(30, ErrorMessage = "نوع شبکه نمی‌تواند بیشتر از 30 کاراکتر باشد")]
        public string NetworkType { get; set; } = string.Empty;

        /// <summary>
        /// نام نمایشی اختیاری (مثلاً اینستاگرام کاری)
        /// </summary>
        [MaxLength(100, ErrorMessage = "نام لینک نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Label { get; set; }

        [Required(ErrorMessage = "مقدار لینک الزامی است")]
        [MaxLength(500, ErrorMessage = "مقدار لینک نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string Value { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
    }
}
